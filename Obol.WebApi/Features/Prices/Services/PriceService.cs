using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Obol.Library.Enums;
using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Instruments.Repositories;
using Obol.WebApi.Features.Instruments.Services;
using Obol.WebApi.Features.Portfolio.Repositories;
using Obol.WebApi.Features.Prices.Repositories;

namespace Obol.WebApi.Features.Prices.Services;

/// <summary>
/// Accounts waiting for their price history (after a sync or an import). One entry per account at a time.
/// </summary>
internal sealed class PriceQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _waiting = new();

    /// <summary>Gets the reader the worker takes accounts from.</summary>
    public ChannelReader<Guid> Reader => _channel.Reader;

    /// <summary>
    /// Asks for an account's prices (nothing happens when it is already waiting).
    /// </summary>
    /// <param name="userId">The account.</param>
    public void Enqueue(Guid userId)
    {
        if (_waiting.TryAdd(userId, 0))
        {
            _channel.Writer.TryWrite(userId);
        }
    }

    /// <summary>
    /// Marks an account as taken off the queue.
    /// </summary>
    /// <param name="userId">The account.</param>
    public void Taken(Guid userId) => _waiting.TryRemove(userId, out _);
}

/// <summary>
/// Fills <c>dbo.PriceDay</c> and <c>dbo.FxDay</c> from Yahoo Finance: the whole history of every instrument an account
/// has held (from its first trade) and of every currency it needs, then the last days on each later run.
/// </summary>
/// <param name="yahoo">Yahoo Finance.</param>
/// <param name="instrumentRepository">Instruments (their Yahoo symbols).</param>
/// <param name="portfolioRepository">The portfolio (what is held, since when).</param>
/// <param name="priceRepository">Prices and rates.</param>
/// <param name="connectionRepository">The account currency.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="options">Module options.</param>
/// <param name="logger">Logger.</param>
internal sealed class PriceService(
    IYahooClient yahoo,
    IInstrumentRepository instrumentRepository,
    IPortfolioRepository portfolioRepository,
    IPriceRepository priceRepository,
    IConnectionRepository connectionRepository,
    IAppClock clock,
    IOptions<Features.ObolOptions> options,
    ILogger<PriceService> logger)
{
    // Trading 212's exchange letter (the last letter before "_EQ") → Yahoo's suffix.
    private static readonly Dictionary<char, string> Exchanges = new()
    {
        ['l'] = ".L",
        ['d'] = ".DE",
        ['a'] = ".AS",
        ['p'] = ".PA",
        ['e'] = ".MC",
        ['m'] = ".MI",
        ['s'] = ".SW",
        ['b'] = ".BR",
    };

    // Yahoo exchanges in order of preference for an instrument priced in a currency.
    private static readonly Dictionary<string, string[]> PreferredExchanges = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EUR"] = ["AMS", "GER", "FRA", "PAR", "MIL", "LIS", "MCE", "EBS", "BRU", "VIE", "DUB"],
        ["USD"] = ["NMS", "NYQ", "NGM", "NCM", "PCX", "ASE", "BTS", "NAS"],
        ["GBX"] = ["LSE", "IOB"],
        ["GBP"] = ["LSE", "IOB"],
        ["CHF"] = ["EBS"],
        ["CAD"] = ["TOR", "VAN"],
    };

    /// <summary>
    /// Fetches what an account's charts need.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many prices were saved.</returns>
    public async Task<int> UpdateAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!options.Value.PriceHistory)
        {
            return 0;
        }

        var today = clock.Today;
        var trades = await portfolioRepository.ListTradesAsync(userId);
        var holdings = await portfolioRepository.ListHoldingsAsync(userId);
        var dividends = await portfolioRepository.ListDividendsAsync(userId);
        var connection = await connectionRepository.GetAsync(userId);
        var quote = (connection?.Currency ?? options.Value.Currency).ToUpperInvariant();

        // From when each instrument is needed: its first trade (or when the position was opened), a week earlier.
        var since = new Dictionary<int, DateOnly>();
        void Need(int id, DateOnly day)
        {
            if (!since.TryGetValue(id, out var known) || day < known)
            {
                since[id] = day;
            }
        }

        foreach (var trade in trades)
        {
            Need(trade.InstrumentID, clock.LocalDay(trade.ExecutedAt).AddDays(-7));
        }

        foreach (var holding in holdings)
        {
            Need(holding.InstrumentID, holding.OpenedAt is { } opened ? clock.LocalDay(opened).AddDays(-7) : today.AddYears(-1));
        }

        foreach (var dividend in dividends)
        {
            Need(dividend.InstrumentID, clock.LocalDay(dividend.PaidAt).AddDays(-7));
        }

        var instruments = (await instrumentRepository.ListAsync()).Where(row => since.ContainsKey(row.InstrumentID)).ToList();
        var references = References(instruments, holdings, trades);
        var saved = 0;
        foreach (var instrument in instruments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            saved += await UpdateInstrumentAsync(instrument, since[instrument.InstrumentID], today, cancellationToken, references.GetValueOrDefault(instrument.InstrumentID));
        }

        if (since.Count > 0)
        {
            var first = since.Values.Min();
            saved += await UpdateBenchmarksAsync(first, today, cancellationToken);
            foreach (var currency in instruments.Select(static row => row.Currency.ToUpperInvariant() == "GBX" ? "GBP" : row.Currency.ToUpperInvariant())
                .Concat(Benchmarks.All.Select(static benchmark => benchmark.Currency))
                .Where(currency => currency != quote && currency.Length == 3).Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                saved += await UpdateFxAsync(currency, quote, first, today, cancellationToken);
            }
        }

        await UpdateProfilesAsync(instruments, cancellationToken);
        return saved;
    }

    /// <summary>
    /// The daily closes of the benchmark indexes from <paramref name="from"/> (shared by every account).
    /// </summary>
    /// <param name="from">The first day needed.</param>
    /// <param name="today">Today.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The prices saved.</returns>
    internal async Task<int> UpdateBenchmarksAsync(DateOnly from, DateOnly today, CancellationToken cancellationToken)
    {
        var saved = 0;
        foreach (var row in await instrumentRepository.UpsertAsync(Benchmarks.Inputs))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Benchmarks.All.FirstOrDefault(benchmark => benchmark.Key == row.InstrumentKey) is not { } benchmark)
            {
                continue;
            }

            if (row.PriceSymbol != benchmark.YahooSymbol)
            {
                await instrumentRepository.SetPriceSymbolAsync(row.InstrumentID, benchmark.YahooSymbol, null);
                row.PriceSymbol = benchmark.YahooSymbol;
            }

            saved += await UpdateInstrumentAsync(row, from, today, cancellationToken);
        }

        return saved;
    }

    /// <summary>
    /// The sector and industry of the shares that don't have one yet, from Yahoo Finance's search (once per share).
    /// Funds are grouped by their asset class instead.
    /// </summary>
    private async Task UpdateProfilesAsync(IEnumerable<InstrumentRow> instruments, CancellationToken cancellationToken)
    {
        foreach (var instrument in instruments.Where(static row => row.Sector is null && row.Class == AssetClass.Stock && !row.IsBenchmark))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var query = instrument.Isin is { Length: 12 } isin ? isin : instrument.Symbol;
            if (query.Length == 0)
            {
                continue;
            }

            var quotes = await yahoo.SearchAsync(query, cancellationToken);
            var best = quotes.FirstOrDefault(static quote => quote.Sector.Length > 0);
            await instrumentRepository.SetProfileAsync(instrument.InstrumentID, best?.Sector ?? string.Empty, best?.Industry ?? string.Empty);
        }
    }

    /// <summary>
    /// The Yahoo symbols worth trying for an instrument: from its Trading 212 ticker first.
    /// </summary>
    /// <param name="ticker">The Trading 212 ticker.</param>
    /// <returns>The symbols.</returns>
    internal static List<string> TickerCandidates(string ticker)
    {
        var candidates = new List<string>();
        var value = (ticker ?? string.Empty).Trim();
        if (value.EndsWith("_US_EQ", StringComparison.Ordinal))
        {
            candidates.Add(value[..^6].Replace('.', '-'));
        }
        else if (value.EndsWith("_EQ", StringComparison.Ordinal) && value.Length > 4)
        {
            var head = value[..^3];
            if (char.IsLower(head[^1]) && Exchanges.TryGetValue(head[^1], out var suffix))
            {
                candidates.Add(head[..^1].ToUpperInvariant() + suffix);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Orders Yahoo search results by how likely they are the listing Trading 212 prices: the exchanges of the
    /// instrument's currency first, ETFs and shares before anything else.
    /// </summary>
    /// <param name="quotes">The search results.</param>
    /// <param name="currency">The instrument currency.</param>
    /// <returns>The symbols, best first.</returns>
    internal static List<string> Rank(IEnumerable<YahooQuote> quotes, string currency)
    {
        var preferred = PreferredExchanges.TryGetValue(currency, out var list) ? list : [];
        return quotes
            .Where(static quote => quote.Type is "EQUITY" or "ETF" or "MUTUALFUND" or "")
            .OrderBy(quote => Array.IndexOf(preferred, quote.Exchange) is var index && index >= 0 ? index : 100)
            .Select(static quote => quote.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// How to turn Yahoo's prices into the instrument's currency (pence and pounds), or null when they don't match.
    /// </summary>
    /// <param name="yahooCurrency">Yahoo's currency (GBp for pence).</param>
    /// <param name="instrumentCurrency">The instrument's currency.</param>
    /// <returns>The factor, or null.</returns>
    internal static decimal? Factor(string yahooCurrency, string instrumentCurrency)
    {
        var from = yahooCurrency == "GBp" ? "GBX" : (yahooCurrency ?? string.Empty).ToUpperInvariant();
        var to = (instrumentCurrency ?? string.Empty).ToUpperInvariant();
        return from == to ? 1m
            : from == "GBX" && to == "GBP" ? 0.01m
            : from == "GBP" && to == "GBX" ? 100m
            : null;
    }

    /// <summary>
    /// The price Trading 212 itself gave each instrument most recently, in the instrument's currency: today's price of an
    /// open position, else the price of its last trade in the past year. A Yahoo listing has to agree with it.
    /// </summary>
    /// <param name="instruments">The instruments.</param>
    /// <param name="holdings">The open positions.</param>
    /// <param name="trades">The trades.</param>
    /// <returns>The prices and their day, by instrument.</returns>
    internal Dictionary<int, PriceReference> References(IEnumerable<InstrumentRow> instruments, IEnumerable<Portfolio.Models.Entities.HoldingRow> holdings, IEnumerable<Portfolio.Models.Entities.TradeRow> trades)
    {
        var byId = instruments.ToDictionary(static row => row.InstrumentID);
        var references = new Dictionary<int, PriceReference>();
        foreach (var trade in trades.OrderBy(static trade => trade.ExecutedAt))
        {
            // Only a recent trade: Yahoo's closes are split-adjusted, so an old trade's price can be off by the split.
            if (byId.TryGetValue(trade.InstrumentID, out var instrument) && trade.Price > 0 && clock.LocalDay(trade.ExecutedAt) >= clock.Today.AddDays(-365)
                && Portfolio.Services.PortfolioLoader.SameCurrencyPrice(trade.Price, trade.PriceCurrency, instrument.Currency) is { } price)
            {
                references[trade.InstrumentID] = new PriceReference(price, clock.LocalDay(trade.ExecutedAt));
            }
        }

        foreach (var holding in holdings.Where(static holding => holding.CurrentPrice > 0))
        {
            references[holding.InstrumentID] = new PriceReference(holding.CurrentPrice, Portfolio.Services.PortfolioLoader.MarketDay(clock.LocalDay(holding.UpdatedAt)));
        }

        return references;
    }

    /// <summary>
    /// Whether a Yahoo listing prices the same thing Trading 212 does: its close nearest the reference day (within five
    /// days) is within a factor of 1.5 of Trading 212's price. A listing of another company that kept an old ticker (a
    /// SPAC's, say) is several times off. Without a close near the reference there's nothing to compare, and it passes.
    /// </summary>
    /// <param name="chart">Yahoo's closes.</param>
    /// <param name="factor">From Yahoo's currency to the instrument's.</param>
    /// <param name="reference">Trading 212's price.</param>
    /// <returns>False when the listing is clearly something else.</returns>
    internal static bool Plausible(YahooChart chart, decimal factor, PriceReference? reference)
    {
        if (reference is not { Price: > 0 } known || chart.Closes.Count == 0)
        {
            return true;
        }

        var nearest = chart.Closes.MinBy(pair => Math.Abs(pair.Key.DayNumber - known.Day.DayNumber));
        if (Math.Abs(nearest.Key.DayNumber - known.Day.DayNumber) > 5 || nearest.Value <= 0)
        {
            return true;
        }

        var ratio = nearest.Value * factor / known.Price;
        return ratio is >= 0.66m and <= 1.5m;
    }

    private async Task<int> UpdateInstrumentAsync(InstrumentRow instrument, DateOnly from, DateOnly today, CancellationToken cancellationToken, PriceReference? reference = null)
    {
        var symbol = instrument.PriceSymbol;
        YahooChart? chart = null;
        decimal factor = 1m;

        var needFrom = instrument.PricesFrom is { } pricedFrom && DateOnly.FromDateTime(pricedFrom) <= from ? today.AddDays(-12) : from;
        if (symbol is { Length: > 0 })
        {
            chart = await yahoo.ChartAsync(symbol, needFrom, today, cancellationToken);
            if (chart is null || Factor(chart.Currency, instrument.Currency) is not { } ratio)
            {
                return 0;
            }

            factor = ratio;
            if (!Plausible(chart, factor, reference))
            {
                // The listing kept so far isn't this instrument (its prices are far from Trading 212's): forget it and
                // the closes it gave, and look again.
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Yahoo's {YahooSymbol} doesn't match {Symbol} ({Key}) any more; looking for the right listing", symbol, instrument.Symbol, instrument.InstrumentKey);

                await instrumentRepository.ResetPricesAsync(instrument.InstrumentID);
                symbol = null;
                chart = null;
                needFrom = from;
            }
        }
        else if (symbol is not null)
        {
            return 0;
        }

        if (symbol is null)
        {
            (symbol, chart, factor) = await FindSymbolAsync(instrument, needFrom, today, reference, cancellationToken);
            if (symbol is null)
            {
                await instrumentRepository.SetPriceSymbolAsync(instrument.InstrumentID, string.Empty, null);
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("No Yahoo Finance listing found for {Symbol} ({Key})", instrument.Symbol, instrument.InstrumentKey);

                return 0;
            }
        }

        if (chart is null || chart.Closes.Count == 0)
        {
            return 0;
        }

        var rows = chart.Closes.Where(pair => pair.Key < today)
            .Select(pair => new PriceInput(instrument.InstrumentID, DbTime.Day(pair.Key), pair.Value * factor, (byte)DataSource.Yahoo))
            .ToList();
        await priceRepository.SavePricesAsync(rows);
        var pricesFrom = instrument.PricesFrom is { } was && symbol == instrument.PriceSymbol ? DateOnly.FromDateTime(was) : (DateOnly?)null;
        var first = pricesFrom is null || needFrom < pricesFrom ? needFrom : (DateOnly?)null;
        await instrumentRepository.SetPriceSymbolAsync(instrument.InstrumentID, symbol, first);
        return rows.Count;
    }

    private async Task<(string? Symbol, YahooChart? Chart, decimal Factor)> FindSymbolAsync(
        InstrumentRow instrument, DateOnly from, DateOnly today, PriceReference? reference, CancellationToken cancellationToken)
    {
        // The ISIN names the company whatever its ticker became; the ticker (which can be a SPAC's or an old one) after it.
        var candidates = new List<string>();
        if (instrument.Isin is { Length: 12 } isin)
        {
            candidates.AddRange(Rank(await yahoo.SearchAsync(isin, cancellationToken), instrument.Currency));
        }

        candidates.AddRange(TickerCandidates(instrument.Ticker));
        if (candidates.Count == 0 && instrument.Symbol.Length > 0)
        {
            candidates.AddRange(Rank(await yahoo.SearchAsync(instrument.Symbol, cancellationToken), instrument.Currency));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(5))
        {
            var chart = await yahoo.ChartAsync(candidate, from, today, cancellationToken);
            if (chart is { Closes.Count: > 0 } && Factor(chart.Currency, instrument.Currency) is { } factor && Plausible(chart, factor, reference))
            {
                return (candidate, chart, factor);
            }
        }

        return (null, null, 1m);
    }

    private async Task<int> UpdateFxAsync(string currency, string quote, DateOnly from, DateOnly today, CancellationToken cancellationToken)
    {
        var known = (await priceRepository.ListFxAsync(quote, from.AddDays(-10))).Where(row => row.Currency == currency).ToList();
        var start = known.Count > 0 && DateOnly.FromDateTime(known.Min(static row => row.Day)) <= from.AddDays(5) ? today.AddDays(-12) : from;
        var chart = await yahoo.ChartAsync($"{currency}{quote}=X", start, today, cancellationToken);
        if (chart is null || chart.Closes.Count == 0)
        {
            return 0;
        }

        var rows = chart.Closes.Where(pair => pair.Key < today).Select(pair => new FxInput(currency, quote, DbTime.Day(pair.Key), pair.Value)).ToList();
        await priceRepository.SaveFxAsync(rows);
        return rows.Count;
    }
}

/// <summary>A price Trading 212 gave an instrument, in its currency, and its day.</summary>
/// <param name="Price">The price.</param>
/// <param name="Day">The day.</param>
internal sealed record PriceReference(decimal Price, DateOnly Day);
