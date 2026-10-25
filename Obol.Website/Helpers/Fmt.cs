using System.Globalization;
using Obol.Library.Enums;

namespace Obol.Website.Helpers;

/// <summary>
/// How money, shares, percentages and dates read on the screens.
/// </summary>
public static class Fmt
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    /// <summary>The sign before a currency amount (€, $, £), or the code and a space.</summary>
    /// <param name="currency">The currency.</param>
    /// <returns>The prefix.</returns>
    public static string Symbol(string? currency) => (currency ?? string.Empty).ToUpperInvariant() switch
    {
        "EUR" => "€",
        "USD" => "$",
        "GBP" => "£",
        "JPY" => "¥",
        "CHF" => "CHF ",
        "" => "€",
        var code => code + " ",
    };

    /// <summary>"€11,880.34".</summary>
    /// <param name="value">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="decimals">Decimals.</param>
    /// <returns>The text.</returns>
    public static string Money(decimal value, string? currency, int decimals = 2)
    {
        if ((currency ?? string.Empty).Equals("GBX", StringComparison.OrdinalIgnoreCase))
        {
            return Number(value, decimals) + "p";
        }

        var text = Symbol(currency) + Number(Math.Abs(value), decimals);
        return value < 0 ? "−" + text : text;
    }

    /// <summary>"€11,880" and ".34", for the big numbers (the cents drawn lighter).</summary>
    /// <param name="value">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The whole part (with the sign) and the cents.</returns>
    public static (string Whole, string Cents) MoneyParts(decimal value, string? currency)
    {
        var rounded = Math.Round(Math.Abs(value), 2);
        var whole = Math.Truncate(rounded);
        var cents = (int)Math.Round((rounded - whole) * 100);
        return ((value < 0 ? "−" : string.Empty) + Symbol(currency) + whole.ToString("#,0", English), "." + cents.ToString("00", English));
    }

    /// <summary>"+€48.12", "−€4.20".</summary>
    /// <param name="value">The change.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="decimals">Decimals.</param>
    /// <returns>The text.</returns>
    public static string SignedMoney(decimal value, string? currency, int decimals = 2) =>
        (Math.Round(value, decimals) > 0 ? "+" : Math.Round(value, decimals) < 0 ? "−" : string.Empty) + Money(Math.Abs(value), currency, decimals);

    /// <summary>A compact amount for axes and small labels: "€11.9k", "€840".</summary>
    /// <param name="value">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The text.</returns>
    public static string Compact(decimal value, string? currency)
    {
        var abs = Math.Abs(value);
        var text = abs >= 1_000_000m ? (abs / 1_000_000m).ToString("0.##", English) + "M"
            : abs >= 10_000m ? (abs / 1000m).ToString("0.#", English) + "k"
            : abs >= 1000m ? (abs / 1000m).ToString("0.##", English) + "k"
            : abs.ToString("0", English);
        return (value < 0 ? "−" : string.Empty) + Symbol(currency) + text;
    }

    /// <summary>"18.3%" from a share (0.183).</summary>
    /// <param name="share">The share.</param>
    /// <param name="decimals">Decimals.</param>
    /// <returns>The text, or "—".</returns>
    public static string Pct(decimal? share, int decimals = 1) =>
        share is { } value ? (value * 100m).ToString("0." + new string('0', decimals), English) + "%" : "—";

    /// <summary>"+0.41%", "−4.2%" from a share.</summary>
    /// <param name="share">The share.</param>
    /// <param name="decimals">Decimals.</param>
    /// <returns>The text, or "—".</returns>
    public static string SignedPct(decimal? share, int decimals = 2)
    {
        if (share is not { } value)
        {
            return "—";
        }

        var rounded = Math.Round(value * 100m, decimals);
        return (rounded > 0 ? "+" : rounded < 0 ? "−" : string.Empty) + Math.Abs(rounded).ToString("0." + new string('0', decimals), English) + "%";
    }

    /// <summary>"▲ 0.58%" / "▼ 4.2%".</summary>
    /// <param name="share">The share.</param>
    /// <param name="decimals">Decimals.</param>
    /// <returns>The text.</returns>
    public static string Arrow(decimal? share, int decimals = 2) =>
        share is not { } value ? "—" : (value >= 0 ? "▲ " : "▼ ") + Math.Abs(Math.Round(value * 100m, decimals)).ToString("0." + new string('0', decimals), English) + "%";

    /// <summary>Shares: up to six decimals, trailing zeros dropped ("42.18", "0.004512").</summary>
    /// <param name="value">The shares.</param>
    /// <returns>The text.</returns>
    public static string Qty(decimal? value) => value is { } shares ? shares.ToString(Math.Abs(shares) >= 100 ? "#,0.##" : Math.Abs(shares) >= 1 ? "#,0.####" : "#,0.######", English) : "—";

    /// <summary>A price in its currency, with up to four decimals for small prices.</summary>
    /// <param name="value">The price.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The text.</returns>
    public static string Price(decimal? value, string? currency) =>
        value is not { } price ? "—" : Money(price, currency, price < 1 ? 4 : 2);

    /// <summary>A plain number with thousands separators.</summary>
    /// <param name="value">The number.</param>
    /// <param name="decimals">Decimals.</param>
    /// <returns>The text.</returns>
    public static string Number(decimal value, int decimals = 2) => value.ToString("#,0." + new string('0', decimals), English);

    /// <summary>"Sunday, 25 October".</summary>
    /// <param name="date">The day.</param>
    /// <returns>The text.</returns>
    public static string LongDate(DateOnly date) => date.ToString("dddd, d MMMM", English);

    /// <summary>"1 Oct", or "1 Oct 2025" in another year.</summary>
    /// <param name="date">The day.</param>
    /// <returns>The text.</returns>
    public static string ShortDate(DateOnly date) => date.Year == Clock.Today.Year ? date.ToString("d MMM", English) : date.ToString("d MMM yyyy", English);

    /// <summary>"1 Oct" or "1 Oct 2025" of an instant, in the app's time zone.</summary>
    /// <param name="at">The instant.</param>
    /// <returns>The text.</returns>
    public static string ShortDate(DateTimeOffset at) => ShortDate(DateOnly.FromDateTime(Local(at)));

    /// <summary>"20:10".</summary>
    /// <param name="at">The instant.</param>
    /// <returns>The text.</returns>
    public static string Time(DateTimeOffset at) => Local(at).ToString("HH:mm", English);

    /// <summary>"Today, 20:10", "Yesterday, 09:05", "1 Oct, 09:05".</summary>
    /// <param name="at">The instant.</param>
    /// <returns>The text.</returns>
    public static string When(DateTimeOffset? at)
    {
        if (at is not { } value)
        {
            return "never";
        }

        var local = Local(value);
        var day = DateOnly.FromDateTime(local);
        var today = Clock.Today;
        var prefix = day == today ? "Today" : day == today.AddDays(-1) ? "Yesterday" : ShortDate(day);
        return $"{prefix}, {local:HH:mm}";
    }

    /// <summary>"Oct" of a month number.</summary>
    /// <param name="month">1–12.</param>
    /// <returns>The text.</returns>
    public static string Month(int month) => new DateOnly(2000, Math.Clamp(month, 1, 12), 1).ToString("MMM", English);

    /// <summary>"OCT" and "13" for a calendar tile.</summary>
    /// <param name="date">The day.</param>
    /// <returns>The month (upper case) and the day.</returns>
    public static (string Month, string Day) Tile(DateOnly date) => (date.ToString("MMM", English).ToUpperInvariant(), date.Day.ToString(English));

    /// <summary>yyyy-MM-dd.</summary>
    /// <param name="date">The day.</param>
    /// <returns>The text.</returns>
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>"just now", "4 min ago", "2 h ago", "3 days ago".</summary>
    /// <param name="at">The instant.</param>
    /// <returns>The text.</returns>
    public static string Ago(DateTimeOffset? at)
    {
        if (at is not { } value)
        {
            return "never";
        }

        var span = DateTimeOffset.UtcNow - value;
        return span.TotalMinutes < 1 ? "just now"
            : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes} min ago"
            : span.TotalHours < 24 ? $"{(int)span.TotalHours} h ago"
            : $"{(int)span.TotalDays} days ago";
    }

    /// <summary>The label of a chart range (1W, 1M…).</summary>
    /// <param name="range">The range.</param>
    /// <returns>The label.</returns>
    public static string Range(ChartRange range) => range switch
    {
        ChartRange.Week => "1W",
        ChartRange.Month => "1M",
        ChartRange.Quarter => "3M",
        ChartRange.YearToDate => "YTD",
        ChartRange.Year => "1Y",
        _ => "ALL",
    };

    /// <summary>The words of a chart range ("this year", "since you started").</summary>
    /// <param name="range">The range.</param>
    /// <returns>The words.</returns>
    public static string RangeWords(ChartRange range) => range switch
    {
        ChartRange.Week => "this week",
        ChartRange.Month => "this month",
        ChartRange.Quarter => "in 3 months",
        ChartRange.YearToDate => "this year",
        ChartRange.Year => "in 12 months",
        _ => "since you started",
    };

    /// <summary>The query value of a chart range.</summary>
    /// <param name="range">The range.</param>
    /// <returns>The value (1w, 1m…).</returns>
    public static string RangeQuery(ChartRange range) => Range(range).ToLowerInvariant();

    /// <summary>Reads a chart range from the query ("1w", "ytd"…).</summary>
    /// <param name="value">The query value.</param>
    /// <param name="fallback">The range when it's missing or unknown.</param>
    /// <returns>The range.</returns>
    public static ChartRange ParseRange(string? value, ChartRange fallback = ChartRange.Year) => (value ?? string.Empty).ToLowerInvariant() switch
    {
        "1w" => ChartRange.Week,
        "1m" => ChartRange.Month,
        "3m" => ChartRange.Quarter,
        "ytd" => ChartRange.YearToDate,
        "1y" => ChartRange.Year,
        "all" => ChartRange.All,
        _ => fallback,
    };

    /// <summary>The label of an asset class.</summary>
    /// <param name="assetClass">The class.</param>
    /// <returns>The label.</returns>
    public static string ClassLabel(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Stock => "Stock",
        AssetClass.EquityEtf => "Equity ETF",
        AssetClass.BondEtf => "Bond ETF",
        AssetClass.Crypto => "Crypto",
        AssetClass.Commodity => "Commodity",
        _ => "Other",
    };

    /// <summary>The CSS name of an asset class (its colour), or "cash".</summary>
    /// <param name="key">An <see cref="AssetClass"/> name or "Cash".</param>
    /// <returns>eq, bond, stock, crypto, gold, other or cash.</returns>
    public static string ClassCss(string key) => key switch
    {
        nameof(AssetClass.EquityEtf) => "eq",
        nameof(AssetClass.BondEtf) => "bond",
        nameof(AssetClass.Stock) => "stock",
        nameof(AssetClass.Crypto) => "crypto",
        nameof(AssetClass.Commodity) => "gold",
        "Cash" => "cash",
        _ => "other",
    };

    /// <summary>The CSS name of an asset class.</summary>
    /// <param name="assetClass">The class.</param>
    /// <returns>The name.</returns>
    public static string ClassCss(AssetClass assetClass) => ClassCss(assetClass.ToString());

    /// <summary>"up", "down" or "flat" for a change.</summary>
    /// <param name="value">The change.</param>
    /// <returns>The CSS class.</returns>
    public static string Trend(decimal? value) => value is > 0 ? "up" : value is < 0 ? "down" : "flat";

    /// <summary>The words of an activity kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The words.</returns>
    public static string Kind(ActivityKind kind) => kind switch
    {
        ActivityKind.Buy => "Bought",
        ActivityKind.Sell => "Sold",
        ActivityKind.Dividend => "Dividend",
        ActivityKind.Interest => "Interest",
        ActivityKind.Deposit => "Deposit",
        ActivityKind.Withdrawal => "Withdrawal",
        _ => "Fee",
    };

    private static DateTime Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Clock.Zone).DateTime;
}
