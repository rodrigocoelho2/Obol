using Obol.Library.Enums;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Instruments.Services;
using Obol.WebApi.Features.Portfolio.Services;

namespace Obol.WebApi.Features.Trading212;

/// <summary>
/// Turns Trading 212 API answers into Obol's import records. Pure functions (the unit tests feed them sample JSON).
/// </summary>
internal static class Trading212Mapper
{
    /// <summary>
    /// An instrument as Obol keeps it.
    /// </summary>
    /// <param name="instrument">The instrument of a position, order or dividend.</param>
    /// <param name="fallbackTicker">The ticker when the instrument has none.</param>
    /// <param name="fallbackCurrency">The currency when the instrument has none.</param>
    /// <param name="catalogue">Trading 212's instrument list (for the type), when loaded.</param>
    /// <returns>The instrument, or null without a ticker or ISIN.</returns>
    public static InstrumentInput? Instrument(T212Instrument? instrument, string? fallbackTicker, string? fallbackCurrency, IReadOnlyDictionary<string, T212TradableInstrument>? catalogue)
    {
        var ticker = (instrument?.Ticker ?? fallbackTicker ?? string.Empty).Trim();
        var isin = InstrumentNames.CleanIsin(instrument?.Isin);
        if (ticker.Length == 0 && isin is null)
        {
            return null;
        }

        T212TradableInstrument? known = null;
        catalogue?.TryGetValue(ticker, out known);
        isin ??= InstrumentNames.CleanIsin(known?.Isin);
        var name = FirstText(instrument?.Name, known?.Name, ticker);
        var currency = Currency(FirstText(instrument?.Currency, known?.CurrencyCode, fallbackCurrency, "EUR"));
        var assetClass = InstrumentNames.Classify(known?.Type, name);
        return new InstrumentInput(
            InstrumentNames.Key(isin, ticker),
            isin,
            InstrumentNames.Truncate(ticker.Length > 0 ? ticker : isin, 40),
            InstrumentNames.Symbol(ticker.Length > 0 ? ticker : isin),
            InstrumentNames.Truncate(name, 200),
            currency,
            (byte)assetClass);
    }

    /// <summary>
    /// A filled order as a trade (null for anything that isn't a plain buy or sell: splits, transfers in, corporate actions).
    /// </summary>
    /// <param name="item">The order and its fill.</param>
    /// <param name="instrumentKey">The key of its instrument.</param>
    /// <param name="instrumentCurrency">The instrument's currency.</param>
    /// <returns>The trade, or null.</returns>
    public static ImportedTrade? Trade(T212HistoricalOrder item, string instrumentKey, string instrumentCurrency)
    {
        var fill = item.Fill;
        var order = item.Order;
        if (fill is null || fill.Quantity == 0 || fill.Price <= 0 || fill.FilledAt is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(fill.Type) && !string.Equals(fill.Type, "TRADE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var wallet = fill.WalletImpact;
        var buy = order?.Side is { Length: > 0 } side
            ? string.Equals(side, "BUY", StringComparison.OrdinalIgnoreCase)
            : fill.Quantity > 0 && (wallet?.NetValue ?? -1) <= 0;
        var fees = wallet?.Taxes?.Sum(static tax => Math.Abs(tax.Quantity)) ?? 0m;
        var total = Math.Abs(wallet?.NetValue ?? 0m);
        if (total == 0)
        {
            total = Math.Abs(fill.Quantity * fill.Price);
        }

        return new ImportedTrade(
            instrumentKey,
            buy,
            Math.Abs(fill.Quantity),
            fill.Price,
            Currency(instrumentCurrency),
            wallet?.FxRate,
            total,
            buy ? null : wallet?.RealisedProfitLoss,
            fees,
            fill.FilledAt.Value.UtcDateTime,
            DataSource.Trading212,
            fill.Id > 0 ? fill.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) : order?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A dividend.
    /// </summary>
    /// <param name="item">The dividend.</param>
    /// <param name="instrumentKey">The key of its instrument.</param>
    /// <returns>The dividend.</returns>
    public static ImportedDividend Dividend(T212Dividend item, string instrumentKey) => new(
        instrumentKey,
        item.PaidOn.UtcDateTime,
        item.Quantity is { } quantity && quantity != 0 ? Math.Abs(quantity) : null,
        item.GrossAmountPerShare,
        item.Amount,
        null,
        string.IsNullOrWhiteSpace(item.Type) ? "DIVIDEND" : item.Type.Trim().ToUpperInvariant(),
        DataSource.Trading212);

    /// <summary>
    /// A deposit, withdrawal, interest payment or fee (null for anything else).
    /// </summary>
    /// <param name="item">The transaction.</param>
    /// <returns>The cash movement, or null.</returns>
    public static ImportedCash? Cash(T212Transaction item)
    {
        var amount = Math.Abs(item.Amount);
        if (amount == 0)
        {
            return null;
        }

        var type = (item.Type ?? string.Empty).Trim().ToUpperInvariant();
        var at = item.DateTime.UtcDateTime;
        return type switch
        {
            "DEPOSIT" => new ImportedCash(CashKind.Deposit, amount, at, "Deposit", DataSource.Trading212),
            "WITHDRAW" or "WITHDRAWAL" => new ImportedCash(CashKind.Withdrawal, -amount, at, "Withdrawal", DataSource.Trading212),
            "INTEREST_ON_FREE_CASH" => new ImportedCash(CashKind.Interest, amount, at, "Interest on cash", DataSource.Trading212),
            "LENDING_INTEREST" => new ImportedCash(CashKind.Interest, amount, at, "Share lending interest", DataSource.Trading212),
            "FEE" => new ImportedCash(CashKind.Fee, -amount, at, "Fee", DataSource.Trading212),
            "TRANSFER" => new ImportedCash(CashKind.Other, item.Amount, at, "Transfer", DataSource.Trading212),
            _ => null,
        };
    }

    /// <summary>
    /// A currency code in upper case ("GBp" pence becomes GBX).
    /// </summary>
    /// <param name="currency">The code.</param>
    /// <returns>The code.</returns>
    public static string Currency(string? currency)
    {
        var value = (currency ?? string.Empty).Trim();
        if (value == "GBp")
        {
            return "GBX";
        }

        value = value.ToUpperInvariant();
        return value.Length == 3 ? value : "EUR";
    }

    private static string FirstText(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
