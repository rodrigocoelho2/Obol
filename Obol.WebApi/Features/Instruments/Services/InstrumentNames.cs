using System.Text.RegularExpressions;
using Obol.Library.Enums;

namespace Obol.WebApi.Features.Instruments.Services;

/// <summary>
/// Keys, symbols and asset classes of instruments, from what Trading 212 (API or CSV) says about them.
/// </summary>
internal static partial class InstrumentNames
{
    private static readonly string[] EtfWords =
    [
        " ETF", "UCITS", "ISHARES", "VANGUARD", "XTRACKERS", "SPDR", "AMUNDI", "INVESCO", "LYXOR", "WISDOMTREE", "VANECK",
        "HSBC MSCI", "FRANKLIN", "JPMORGAN ETF", "GLOBAL X", " ETC ",
    ];

    private static readonly string[] BondWords =
    [
        "BOND", "GOVT", "GOVERNMENT", "TREASURY", "TREASURIES", "AGGREGATE", "AGG ", "CORP ", "CORPORATE", "GILT", "T-BILL",
        "INFLATION", "FIXED INCOME", "MONEY MARKET", "OVERNIGHT", "ULTRASHORT", "SHORT DURATION",
    ];

    private static readonly string[] CommodityWords = [" GOLD", " SILVER", " PLATINUM", "COMMODIT", " OIL ", "PHYSICAL"];

    /// <summary>
    /// The natural key of an instrument: its ISIN, or "T212:" + the ticker when it has none.
    /// </summary>
    /// <param name="isin">The ISIN.</param>
    /// <param name="ticker">The ticker.</param>
    /// <returns>The key.</returns>
    public static string Key(string? isin, string? ticker)
    {
        var cleanIsin = CleanIsin(isin);
        if (cleanIsin is not null)
        {
            return cleanIsin;
        }

        var t = (ticker ?? string.Empty).Trim();
        return Truncate("T212:" + (t.Length == 0 ? "UNKNOWN" : t), 40);
    }

    /// <summary>
    /// A valid ISIN in upper case, or null.
    /// </summary>
    /// <param name="isin">The text.</param>
    /// <returns>The ISIN.</returns>
    public static string? CleanIsin(string? isin)
    {
        var value = (isin ?? string.Empty).Trim().ToUpperInvariant();
        return IsinPattern().IsMatch(value) ? value : null;
    }

    /// <summary>
    /// The symbol to show for an instrument: for a US share whose Yahoo Finance listing (checked against Trading 212's
    /// price) has another ticker than Trading 212's, the market's ticker (ACHR rather than ACIC); otherwise the symbol.
    /// Other markets keep Trading 212's symbol: their Yahoo listing can be another exchange's line of the same fund.
    /// </summary>
    /// <param name="symbol">The symbol from Trading 212's ticker.</param>
    /// <param name="ticker">Trading 212's ticker.</param>
    /// <param name="priceSymbol">The Yahoo Finance symbol, when found.</param>
    /// <returns>The symbol to show.</returns>
    public static string DisplaySymbol(string symbol, string? ticker, string? priceSymbol)
    {
        if (string.IsNullOrWhiteSpace(priceSymbol) || ticker is null || !ticker.EndsWith("_US_EQ", StringComparison.Ordinal)
            || priceSymbol.Contains('.', StringComparison.Ordinal) || priceSymbol.Contains('=', StringComparison.Ordinal) || priceSymbol.Contains('^', StringComparison.Ordinal))
        {
            return symbol;
        }

        // Yahoo writes share classes with a dash (BRK-B), Trading 212 with a dot or a slash.
        var market = priceSymbol.Trim().ToUpperInvariant().Replace('-', '.');
        var mine = symbol.ToUpperInvariant().Replace('/', '.').Replace('-', '.');
        return market.Length is > 0 and <= 12 && market != mine ? market : symbol;
    }

    /// <summary>
    /// The symbol to show for a Trading 212 ticker: "AAPL_US_EQ" → AAPL, "VUSAl_EQ" → VUSA, "IWDAa_EQ" → IWDA.
    /// A plain symbol (from a CSV export) is kept as it is.
    /// </summary>
    /// <param name="ticker">The ticker.</param>
    /// <returns>The symbol.</returns>
    public static string Symbol(string? ticker)
    {
        var value = (ticker ?? string.Empty).Trim();
        var underscore = value.IndexOf('_', StringComparison.Ordinal);
        if (underscore <= 0)
        {
            return Truncate(value.ToUpperInvariant(), 20);
        }

        var head = value[..underscore];
        // Non-US tickers end with the exchange as one lower-case letter (l London, d Xetra, a Amsterdam, p Paris…).
        if (head.Length > 1 && char.IsLower(head[^1]) && head[..^1].Any(char.IsUpper))
        {
            head = head[..^1];
        }

        return Truncate(head.ToUpperInvariant(), 20);
    }

    /// <summary>
    /// The asset class from Trading 212's instrument type (STOCK, ETF…) and the name.
    /// </summary>
    /// <param name="type">The type, when known.</param>
    /// <param name="name">The name.</param>
    /// <returns>The asset class.</returns>
    public static AssetClass Classify(string? type, string? name)
    {
        var upper = " " + (name ?? string.Empty).ToUpperInvariant() + " ";
        switch ((type ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "STOCK":
                return AssetClass.Stock;
            case "ETF":
                return Fund(upper);
            case "CRYPTO":
            case "CRYPTOCURRENCY":
                return AssetClass.Crypto;
            case "WARRANT":
            case "CVR":
            case "CORPACT":
            case "FUTURES":
            case "FOREX":
            case "INDEX":
                return AssetClass.Other;
        }

        // No type (a CSV export): guess from the name.
        return EtfWords.Any(upper.Contains) ? Fund(upper) : AssetClass.Stock;
    }

    /// <summary>
    /// Shortens a text to a column's length.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="length">The maximum length.</param>
    /// <returns>The text.</returns>
    public static string Truncate(string? value, int length)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= length ? text : text[..length];
    }

    private static AssetClass Fund(string upperName) =>
        BondWords.Any(upperName.Contains) ? AssetClass.BondEtf
        : CommodityWords.Any(upperName.Contains) ? AssetClass.Commodity
        : AssetClass.EquityEtf;

    [GeneratedRegex("^[A-Z]{2}[A-Z0-9]{9}[0-9]$")]
    private static partial Regex IsinPattern();
}
