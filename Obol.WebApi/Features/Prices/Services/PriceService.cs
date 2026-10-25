using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Obol.Library.Enums;
using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Instruments.Repositories;
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
        var saved = 0;
        foreach (var instrument in instruments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            saved += await UpdateInstrumentAsync(instrument, since[instrument.InstrumentID], today, cancellationToken);
        }

        if (since.Count > 0)
        {
            var first = since.Values.Min();
            foreach (var currency in instruments.Select(static row => row.Currency.ToUpperInvariant() == "GBX" ? "GBP" : row.Currency.ToUpperInvariant())
                .Where(currency => currency != quote && currency.Length == 3).Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                saved += await UpdateFxAsync(currency, quote, first, today, cancellationToken);
            }
        }

        return saved;
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

    private async Task<int> UpdateInstrumentAsync(InstrumentRow instrument, DateOnly from, DateOnly today, CancellationToken cancellationToken)
    {
        var symbol = instrument.PriceSymbol;
        YahooChart? chart = null;
        decimal factor = 1m;

        var needFrom = instrument.PricesFrom is { } pricedFrom && DateOnly.FromDateTime(pricedFrom) <= from ? today.AddDays(-12) : from;
        if (symbol is null)
        {
            (symbol, chart, factor) = await FindSymbolAsync(instrument, needFrom, today, cancellationToken);
            if (symbol is null)
            {
                await instrumentRepository.SetPriceSymbolAsync(instrument.InstrumentID, string.Empty, null);
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("No Yahoo Finance listing found for {Symbol} ({Key})", instrument.Symbol, instrument.InstrumentKey);

                return 0;
            }
        }
        else if (symbol.Length == 0)
        {
            return 0;
        }
        else
        {
            chart = await yahoo.ChartAsync(symbol, needFrom, today, cancellationToken);
            if (chart is null || Factor(chart.Currency, instrument.Currency) is not { } ratio)
            {
                return 0;
            }

            factor = ratio;
        }

        if (chart is null || chart.Closes.Count == 0)
        {
            return 0;
        }

        var rows = chart.Closes.Where(pair => pair.Key < today)
            .Select(pair => new PriceInput(instrument.InstrumentID, DbTime.Day(pair.Key), pair.Value * factor, (byte)DataSource.Yahoo))
            .ToList();
        await priceRepository.SavePricesAsync(rows);
        var first = needFrom < (instrument.PricesFrom is { } was ? DateOnly.FromDateTime(was) : needFrom) ? needFrom : (DateOnly?)null;
        await instrumentRepository.SetPriceSymbolAsync(instrument.InstrumentID, symbol, instrument.PricesFrom is null ? needFrom : first);
        return rows.Count;
    }

    private async Task<(string? Symbol, YahooChart? Chart, decimal Factor)> FindSymbolAsync(InstrumentRow instrument, DateOnly from, DateOnly today, CancellationToken cancellationToken)
    {
        var candidates = TickerCandidates(instrument.Ticker);
        if (instrument.Isin is { Length: 12 } isin)
        {
            candidates.AddRange(Rank(await yahoo.SearchAsync(isin, cancellationToken), instrument.Currency));
        }

        if (candidates.Count == 0 && instrument.Symbol.Length > 0)
        {
            candidates.AddRange(Rank(await yahoo.SearchAsync(instrument.Symbol, cancellationToken), instrument.Currency));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(5))
        {
            var chart = await yahoo.ChartAsync(candidate, from, today, cancellationToken);
            if (chart is { Closes.Count: > 0 } && Factor(chart.Currency, instrument.Currency) is { } factor)
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
