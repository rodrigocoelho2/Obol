using System.Text.Json.Serialization;

namespace Obol.WebApi.Features.Portfolio.Models.Entities;

/// <summary>An open position as Trading 212 last reported it (dbo.Holding).</summary>
public sealed class HoldingRow
{
    /// <summary>Gets or sets the instrument.</summary>
    public int InstrumentID { get; set; }

    /// <summary>Gets or sets the number of shares.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the average price paid, in the instrument currency.</summary>
    public decimal AveragePrice { get; set; }

    /// <summary>Gets or sets the current price, in the instrument currency.</summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>Gets or sets the value, in the account currency.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets what was paid, in the account currency.</summary>
    public decimal Cost { get; set; }

    /// <summary>Gets or sets the unrealised gain, in the account currency.</summary>
    public decimal UnrealizedPL { get; set; }

    /// <summary>Gets or sets how much of the gain is the exchange rate.</summary>
    public decimal? FxImpact { get; set; }

    /// <summary>Gets or sets when the position was opened (UTC).</summary>
    public DateTime? OpenedAt { get; set; }

    /// <summary>Gets or sets when it was last reported (UTC).</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A buy or a sell (dbo.Trade).</summary>
public sealed class TradeRow
{
    /// <summary>Gets or sets the internal id.</summary>
    public long TradeID { get; set; }

    /// <summary>Gets or sets the instrument.</summary>
    public int InstrumentID { get; set; }

    /// <summary>Gets or sets the key that makes the same fill from the API and a CSV one row.</summary>
    public string TradeKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the side: 1 buy, 2 sell.</summary>
    public byte Side { get; set; }

    /// <summary>Gets or sets the number of shares (positive).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the price per share, in <see cref="PriceCurrency"/>.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the currency of the price.</summary>
    public string PriceCurrency { get; set; } = string.Empty;

    /// <summary>Gets or sets the exchange rate the broker applied, when there was one.</summary>
    public decimal? FxRate { get; set; }

    /// <summary>Gets or sets what was paid (buy) or received (sell), in the account currency.</summary>
    public decimal Total { get; set; }

    /// <summary>Gets or sets the realised gain of a sell, in the account currency.</summary>
    public decimal? RealizedPL { get; set; }

    /// <summary>Gets or sets the fees and taxes, in the account currency.</summary>
    public decimal Fees { get; set; }

    /// <summary>Gets or sets when it was filled (UTC).</summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>Gets or sets where it came from (<see cref="Library.Enums.DataSource"/>).</summary>
    public byte Source { get; set; }

    /// <summary>Gets or sets the id at the broker.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Gets a value indicating whether this is a buy.</summary>
    [JsonIgnore]
    public bool IsBuy => Side == 1;
}

/// <summary>A dividend (dbo.Dividend).</summary>
public sealed class DividendRow
{
    /// <summary>Gets or sets the internal id.</summary>
    public long DividendID { get; set; }

    /// <summary>Gets or sets the instrument.</summary>
    public int InstrumentID { get; set; }

    /// <summary>Gets or sets the key.</summary>
    public string DividendKey { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was paid (UTC).</summary>
    public DateTime PaidAt { get; set; }

    /// <summary>Gets or sets the shares it was paid on.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Gets or sets the gross amount per share, in the instrument currency.</summary>
    public decimal? PerShare { get; set; }

    /// <summary>Gets or sets the net amount, in the account currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the tax withheld, in the account currency.</summary>
    public decimal? Withheld { get; set; }

    /// <summary>Gets or sets the kind (ORDINARY, INTEREST…).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets where it came from.</summary>
    public byte Source { get; set; }
}

/// <summary>A deposit, withdrawal, interest payment or fee (dbo.CashMovement).</summary>
public sealed class CashMovementRow
{
    /// <summary>Gets or sets the internal id.</summary>
    public long CashMovementID { get; set; }

    /// <summary>Gets or sets the key.</summary>
    public string MovementKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind (<see cref="Library.Enums.CashKind"/>).</summary>
    public byte Kind { get; set; }

    /// <summary>Gets or sets the amount: positive in, negative out, in the account currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets when (UTC).</summary>
    public DateTime At { get; set; }

    /// <summary>Gets or sets a note (the bank, the kind of interest…).</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>Gets or sets where it came from.</summary>
    public byte Source { get; set; }
}

/// <summary>The account as Trading 212 reported it on a day (dbo.AccountSnapshot).</summary>
public sealed class SnapshotRow
{
    /// <summary>Gets or sets the day.</summary>
    public DateTime Day { get; set; }

    /// <summary>Gets or sets the total value.</summary>
    public decimal TotalValue { get; set; }

    /// <summary>Gets or sets the cash.</summary>
    public decimal Cash { get; set; }

    /// <summary>Gets or sets what the open positions cost.</summary>
    public decimal Cost { get; set; }

    /// <summary>Gets or sets the unrealised gain.</summary>
    public decimal UnrealizedPL { get; set; }

    /// <summary>Gets or sets the realised gain.</summary>
    public decimal RealizedPL { get; set; }
}

/// <summary>An open position for dbo.HoldingReplace.</summary>
/// <param name="InstrumentID">The instrument.</param>
/// <param name="Quantity">Shares.</param>
/// <param name="AveragePrice">Average price paid.</param>
/// <param name="CurrentPrice">Current price.</param>
/// <param name="Value">Value.</param>
/// <param name="Cost">Cost.</param>
/// <param name="UnrealizedPL">Unrealised gain.</param>
/// <param name="FxImpact">FX part of the gain.</param>
/// <param name="OpenedAt">Opened at, UTC (yyyy-MM-ddTHH:mm:ss).</param>
public sealed record HoldingInput(
    [property: JsonPropertyName("i")] int InstrumentID,
    [property: JsonPropertyName("q")] decimal Quantity,
    [property: JsonPropertyName("ap")] decimal AveragePrice,
    [property: JsonPropertyName("cp")] decimal CurrentPrice,
    [property: JsonPropertyName("v")] decimal Value,
    [property: JsonPropertyName("c")] decimal Cost,
    [property: JsonPropertyName("u")] decimal UnrealizedPL,
    [property: JsonPropertyName("fx")] decimal? FxImpact,
    [property: JsonPropertyName("o")] string? OpenedAt);

/// <summary>A trade for dbo.TradeSave.</summary>
/// <param name="InstrumentID">The instrument.</param>
/// <param name="Key">The trade key.</param>
/// <param name="Side">1 buy, 2 sell.</param>
/// <param name="Quantity">Shares.</param>
/// <param name="Price">Price per share.</param>
/// <param name="PriceCurrency">Currency of the price.</param>
/// <param name="FxRate">Exchange rate.</param>
/// <param name="Total">Paid or received, account currency.</param>
/// <param name="RealizedPL">Realised gain.</param>
/// <param name="Fees">Fees.</param>
/// <param name="ExecutedAt">Filled at, UTC (yyyy-MM-ddTHH:mm:ss).</param>
/// <param name="Source">Data source.</param>
/// <param name="ExternalId">Id at the broker.</param>
public sealed record TradeInput(
    [property: JsonPropertyName("i")] int InstrumentID,
    [property: JsonPropertyName("k")] string Key,
    [property: JsonPropertyName("sd")] byte Side,
    [property: JsonPropertyName("q")] decimal Quantity,
    [property: JsonPropertyName("p")] decimal Price,
    [property: JsonPropertyName("pc")] string PriceCurrency,
    [property: JsonPropertyName("fx")] decimal? FxRate,
    [property: JsonPropertyName("t")] decimal Total,
    [property: JsonPropertyName("r")] decimal? RealizedPL,
    [property: JsonPropertyName("f")] decimal Fees,
    [property: JsonPropertyName("x")] string ExecutedAt,
    [property: JsonPropertyName("s")] byte Source,
    [property: JsonPropertyName("e")] string? ExternalId);

/// <summary>A dividend for dbo.DividendSave.</summary>
/// <param name="InstrumentID">The instrument.</param>
/// <param name="Key">The key.</param>
/// <param name="PaidAt">Paid at, UTC.</param>
/// <param name="Quantity">Shares.</param>
/// <param name="PerShare">Gross per share.</param>
/// <param name="Amount">Net amount, account currency.</param>
/// <param name="Withheld">Tax withheld.</param>
/// <param name="Kind">Kind.</param>
/// <param name="Source">Data source.</param>
public sealed record DividendInput(
    [property: JsonPropertyName("i")] int InstrumentID,
    [property: JsonPropertyName("k")] string Key,
    [property: JsonPropertyName("p")] string PaidAt,
    [property: JsonPropertyName("q")] decimal? Quantity,
    [property: JsonPropertyName("ps")] decimal? PerShare,
    [property: JsonPropertyName("a")] decimal Amount,
    [property: JsonPropertyName("w")] decimal? Withheld,
    [property: JsonPropertyName("kd")] string Kind,
    [property: JsonPropertyName("s")] byte Source);

/// <summary>A cash movement for dbo.CashMovementSave.</summary>
/// <param name="Key">The key.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Amount">Signed amount.</param>
/// <param name="At">When, UTC.</param>
/// <param name="Note">A note.</param>
/// <param name="Source">Data source.</param>
public sealed record CashInput(
    [property: JsonPropertyName("k")] string Key,
    [property: JsonPropertyName("kd")] byte Kind,
    [property: JsonPropertyName("a")] decimal Amount,
    [property: JsonPropertyName("at")] string At,
    [property: JsonPropertyName("n")] string Note,
    [property: JsonPropertyName("s")] byte Source);
