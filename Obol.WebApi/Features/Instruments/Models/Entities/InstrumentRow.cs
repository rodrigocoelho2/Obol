using System.Text.Json.Serialization;
using Obol.Library.Enums;

namespace Obol.WebApi.Features.Instruments.Models.Entities;

/// <summary>
/// A share, an ETF or anything else someone holds (dbo.Instrument), shared by every account.
/// </summary>
public sealed class InstrumentRow
{
    /// <summary>Gets or sets the internal id.</summary>
    public int InstrumentID { get; set; }

    /// <summary>Gets or sets the natural key: the ISIN, or "T212:" + the ticker.</summary>
    public string InstrumentKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISIN.</summary>
    public string? Isin { get; set; }

    /// <summary>Gets or sets the Trading 212 ticker (AAPL_US_EQ) or the CSV symbol.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>Gets or sets the symbol the screens show (AAPL).</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency prices are in (GBX for pence).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the asset class (<see cref="Library.Enums.AssetClass"/>).</summary>
    public byte AssetClass { get; set; }

    /// <summary>Gets or sets the Yahoo Finance symbol ('' = none found; null = not looked up).</summary>
    public string? PriceSymbol { get; set; }

    /// <summary>Gets or sets the first day prices were fetched from.</summary>
    public DateTime? PricesFrom { get; set; }

    /// <summary>Gets the asset class.</summary>
    [JsonIgnore]
    public AssetClass Class => (AssetClass)AssetClass;
}

/// <summary>
/// An instrument as an import describes it (JSON for dbo.InstrumentUpsert).
/// </summary>
/// <param name="Key">The natural key.</param>
/// <param name="Isin">The ISIN.</param>
/// <param name="Ticker">The broker ticker.</param>
/// <param name="Symbol">The display symbol.</param>
/// <param name="Name">The name.</param>
/// <param name="Currency">The price currency.</param>
/// <param name="AssetClass">The asset class (0 = unknown).</param>
public sealed record InstrumentInput(
    [property: JsonPropertyName("k")] string Key,
    [property: JsonPropertyName("i")] string? Isin,
    [property: JsonPropertyName("t")] string Ticker,
    [property: JsonPropertyName("s")] string Symbol,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("c")] string Currency,
    [property: JsonPropertyName("a")] byte AssetClass);
