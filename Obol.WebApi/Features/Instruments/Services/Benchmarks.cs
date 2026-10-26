using Obol.Library.Enums;
using Obol.WebApi.Features.Instruments.Models.Entities;

namespace Obol.WebApi.Features.Instruments.Services;

/// <summary>One index the returns are compared with.</summary>
/// <param name="Key">The instrument key (BENCH:…).</param>
/// <param name="Label">The short name on the screens.</param>
/// <param name="Name">What it is.</param>
/// <param name="YahooSymbol">The listing its daily closes come from (an ETF that follows it, in euros).</param>
/// <param name="Currency">The listing's currency.</param>
internal sealed record Benchmark(string Key, string Label, string Name, string YahooSymbol, string Currency);

/// <summary>
/// The benchmark indexes: kept as instruments (never held by anyone) so their daily closes go through the same price
/// tables as everything else. Priced through the euro listings of the iShares ETFs that follow them, so a deposit in
/// euros buys "the index" exactly as an investor in euros would have.
/// </summary>
internal static class Benchmarks
{
    /// <summary>The key prefix of a benchmark instrument.</summary>
    public const string KeyPrefix = "BENCH:";

    /// <summary>MSCI World (iShares Core MSCI World, Xetra, EUR).</summary>
    public static readonly Benchmark World = new(KeyPrefix + "MSCIWORLD", "MSCI World", "MSCI World · iShares Core MSCI World (EUNL, EUR)", "EUNL.DE", "EUR");

    /// <summary>S&amp;P 500 (iShares Core S&amp;P 500, Xetra, EUR).</summary>
    public static readonly Benchmark SP500 = new(KeyPrefix + "SP500", "S&P 500", "S&P 500 · iShares Core S&P 500 (SXR8, EUR)", "SXR8.DE", "EUR");

    /// <summary>Every benchmark, in the order the screens show them.</summary>
    public static readonly IReadOnlyList<Benchmark> All = [World, SP500];

    /// <summary>The instrument rows to upsert.</summary>
    public static IReadOnlyList<InstrumentInput> Inputs => All.Select(static benchmark =>
        new InstrumentInput(benchmark.Key, null, benchmark.YahooSymbol, benchmark.Label, benchmark.Name, benchmark.Currency, (byte)AssetClass.Other)).ToList();
}
