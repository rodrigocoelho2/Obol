using Microsoft.Extensions.Options;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Instruments.Repositories;
using Obol.WebApi.Features.Portfolio.Repositories;
using Obol.WebApi.Features.Prices.Repositories;

namespace Obol.WebApi.Features.Portfolio.Services;

/// <summary>
/// Loads everything about an account into a <see cref="PortfolioData"/>: the rows, plus a <see cref="PriceBook"/> made of
/// the daily prices kept, the prices of the trades themselves and today's prices from the last sync.
/// </summary>
/// <param name="portfolioRepository">The portfolio.</param>
/// <param name="instrumentRepository">Instruments.</param>
/// <param name="priceRepository">Prices and exchange rates.</param>
/// <param name="connectionRepository">The Trading 212 connection (currency, whether positions are synced).</param>
/// <param name="clock">The app's clock.</param>
/// <param name="options">Module options (the default currency).</param>
internal sealed class PortfolioLoader(
    IPortfolioRepository portfolioRepository,
    IInstrumentRepository instrumentRepository,
    IPriceRepository priceRepository,
    IConnectionRepository connectionRepository,
    IAppClock clock,
    IOptions<ObolOptions> options)
{
    /// <summary>
    /// Loads an account.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The data.</returns>
    public async Task<PortfolioData> LoadAsync(Guid userId)
    {
        var connection = await connectionRepository.GetAsync(userId);
        var holdings = await portfolioRepository.ListHoldingsAsync(userId);
        var trades = await portfolioRepository.ListTradesAsync(userId);
        var dividends = await portfolioRepository.ListDividendsAsync(userId);
        var cash = await portfolioRepository.ListCashAsync(userId);
        var snapshots = await portfolioRepository.ListSnapshotsAsync(userId);
        var instruments = (await instrumentRepository.ListAsync()).ToDictionary(static row => row.InstrumentID);

        var currency = (connection?.Currency ?? options.Value.Currency).Trim().ToUpperInvariant();
        var today = clock.Today;
        var book = new PriceBook(currency);

        var used = holdings.Select(static row => row.InstrumentID).Concat(trades.Select(static row => row.InstrumentID))
            .Concat(dividends.Select(static row => row.InstrumentID)).Distinct().ToList();
        var firstTrade = trades.Count > 0 ? clock.LocalDay(trades[0].ExecutedAt) : today;
        var from = firstTrade.AddDays(-10);

        foreach (var price in await priceRepository.ListPricesAsync(used, from))
        {
            book.AddPrice(price.InstrumentID, DateOnly.FromDateTime(price.Day), price.Close);
        }

        foreach (var rate in await priceRepository.ListFxAsync(currency, from))
        {
            book.AddFx(rate.Currency, DateOnly.FromDateTime(rate.Day), rate.Rate);
        }

        // The trades are prices too (filling the days with no daily close), and tell the exchange rate of their day.
        foreach (var trade in trades)
        {
            if (!instruments.TryGetValue(trade.InstrumentID, out var instrument) || trade.Quantity <= 0 || trade.Price <= 0)
            {
                continue;
            }

            var day = clock.LocalDay(trade.ExecutedAt);
            var price = SameCurrencyPrice(trade.Price, trade.PriceCurrency, instrument.Currency);
            if (price is { } value)
            {
                book.AddPrice(trade.InstrumentID, day, value, replace: false);
            }

            if (!string.Equals(trade.PriceCurrency, currency, StringComparison.OrdinalIgnoreCase))
            {
                var net = trade.IsBuy ? trade.Total - trade.Fees : trade.Total + trade.Fees;
                var rate = net / (trade.Quantity * trade.Price);
                if (rate > 0)
                {
                    book.AddFx(trade.PriceCurrency, day, rate, replace: false);
                }
            }
        }

        // The last sync: today's price, and the exchange rate Trading 212 values the position at.
        foreach (var holding in holdings)
        {
            if (!instruments.TryGetValue(holding.InstrumentID, out var instrument) || holding.CurrentPrice <= 0)
            {
                continue;
            }

            var day = clock.LocalDay(holding.UpdatedAt);
            book.AddPrice(holding.InstrumentID, day, holding.CurrentPrice);
            if (holding.Quantity > 0 && holding.Value > 0 && !string.Equals(instrument.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                book.AddFx(instrument.Currency, day, holding.Value / (holding.Quantity * holding.CurrentPrice));
            }
        }

        return new PortfolioData
        {
            Currency = currency,
            Today = today,
            Instruments = instruments,
            Holdings = holdings,
            BrokerHoldings = connection is { SyncPositions: true, LastSyncAt: not null },
            Trades = trades,
            Dividends = dividends,
            Cash = cash,
            Snapshots = snapshots,
            Prices = book,
            LocalDay = clock.LocalDay,
        };
    }

    /// <summary>
    /// A trade price in the instrument's currency (pence and pounds converted), or null when the currencies differ.
    /// </summary>
    /// <param name="price">The price.</param>
    /// <param name="priceCurrency">Its currency.</param>
    /// <param name="instrumentCurrency">The instrument's currency.</param>
    /// <returns>The price, or null.</returns>
    internal static decimal? SameCurrencyPrice(decimal price, string priceCurrency, string instrumentCurrency)
    {
        var from = (priceCurrency ?? string.Empty).ToUpperInvariant();
        var to = (instrumentCurrency ?? string.Empty).ToUpperInvariant();
        return from == to ? price
            : from == "GBP" && to == "GBX" ? price * 100m
            : from == "GBX" && to == "GBP" ? price / 100m
            : null;
    }
}
