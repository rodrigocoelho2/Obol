using Obol.Library.Enums;
using Obol.WebApi.Features.Portfolio.Models.DTOs;

namespace Obol.WebApi.Features.Insights.Models.DTOs;

/// <summary>One day of the Performance chart.</summary>
public sealed class PerformancePointDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Day { get; set; }

    /// <summary>Gets or sets the value measured (everything, or what's invested when the transfers aren't known).</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets the money in: the first day's value plus what came in since.</summary>
    public decimal Invested { get; set; }

    /// <summary>Gets or sets what the same money would be worth in each benchmark (same order as the benchmarks).</summary>
    public List<decimal?> Benchmarks { get; set; } = [];
}

/// <summary>The same money in an index.</summary>
public sealed class BenchmarkResultDTO
{
    /// <summary>Gets or sets the key (BENCH:…).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the short name.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets what it is.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether its prices cover the range.</summary>
    public bool Available { get; set; }

    /// <summary>Gets or sets what the money would be worth in it now.</summary>
    public decimal? Value { get; set; }

    /// <summary>Gets or sets the index's own (time-weighted) return over the range.</summary>
    public decimal? Return { get; set; }

    /// <summary>Gets or sets your value minus its value (positive = you're ahead).</summary>
    public decimal? Difference { get; set; }
}

/// <summary>The return of one month.</summary>
public sealed class MonthReturnDTO
{
    /// <summary>Gets or sets the year.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the month (1-12).</summary>
    public int Month { get; set; }

    /// <summary>Gets or sets the time-weighted return.</summary>
    public decimal Return { get; set; }
}

/// <summary>The Performance screen: how the money did, and against the indexes.</summary>
public sealed class PerformanceDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets a value indicating whether there is anything to measure.</summary>
    public bool HasData { get; set; }

    /// <summary>Gets or sets the range.</summary>
    public ChartRange Range { get; set; }

    /// <summary>Gets or sets the first day measured.</summary>
    public DateOnly From { get; set; }

    /// <summary>Gets or sets the number of days measured.</summary>
    public int Days { get; set; }

    /// <summary>Gets or sets a value indicating whether the flows are the deposits and withdrawals (else the trades).</summary>
    public bool FromTransfers { get; set; }

    /// <summary>Gets or sets the value now.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets the money in over the range (the first day's value included).</summary>
    public decimal Invested { get; set; }

    /// <summary>Gets or sets the gain (value minus money in).</summary>
    public decimal Gain { get; set; }

    /// <summary>Gets or sets the money-weighted return a year (XIRR).</summary>
    public decimal? MoneyWeightedYearly { get; set; }

    /// <summary>Gets or sets the money-weighted return over the range.</summary>
    public decimal? MoneyWeighted { get; set; }

    /// <summary>Gets or sets the time-weighted return over the range.</summary>
    public decimal? TimeWeighted { get; set; }

    /// <summary>Gets or sets the time-weighted return a year (ranges of a year or more).</summary>
    public decimal? TimeWeightedYearly { get; set; }

    /// <summary>Gets or sets the chart.</summary>
    public List<PerformancePointDTO> Series { get; set; } = [];

    /// <summary>Gets or sets the benchmarks.</summary>
    public List<BenchmarkResultDTO> Benchmarks { get; set; } = [];

    /// <summary>Gets or sets the monthly returns.</summary>
    public List<MonthReturnDTO> Months { get; set; } = [];
}

/// <summary>A slice of the portfolio (a currency, a sector, a country).</summary>
public sealed class ExposureSliceDTO
{
    /// <summary>Gets or sets the key (currency code, sector, ISO country code).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the value.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets the share of the total.</summary>
    public decimal Percent { get; set; }

    /// <summary>Gets or sets the number of positions in it.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets the symbols in it, biggest first.</summary>
    public List<string> Symbols { get; set; } = [];

    /// <summary>Gets or sets what the exchange rate did to it since bought (currencies only).</summary>
    public decimal? FxImpact { get; set; }
}

/// <summary>How concentrated the portfolio is.</summary>
public sealed class ConcentrationDTO
{
    /// <summary>Gets or sets the number of positions.</summary>
    public int Positions { get; set; }

    /// <summary>Gets or sets the biggest position's symbol.</summary>
    public string TopSymbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the biggest position's weight.</summary>
    public decimal TopWeight { get; set; }

    /// <summary>Gets or sets the weight of the five biggest.</summary>
    public decimal Top5Weight { get; set; }

    /// <summary>Gets or sets the effective number of positions (1 / sum of squared weights).</summary>
    public decimal Effective { get; set; }
}

/// <summary>The Exposure screen: currencies, sectors, countries and concentration.</summary>
public sealed class ExposureDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the total (positions and cash).</summary>
    public decimal Total { get; set; }

    /// <summary>Gets or sets the positions' value.</summary>
    public decimal Invested { get; set; }

    /// <summary>Gets or sets the currencies (cash in the account currency included).</summary>
    public List<ExposureSliceDTO> Currencies { get; set; } = [];

    /// <summary>Gets or sets what exchange rates added to or took from the positions since bought.</summary>
    public decimal FxImpact { get; set; }

    /// <summary>Gets or sets the value in other currencies (what a 1 % move of the euro changes by a hundredth).</summary>
    public decimal Foreign { get; set; }

    /// <summary>Gets or sets the sectors (shares by sector, funds by kind).</summary>
    public List<ExposureSliceDTO> Sectors { get; set; } = [];

    /// <summary>Gets or sets the countries (where each share is from; funds apart).</summary>
    public List<ExposureSliceDTO> Countries { get; set; } = [];

    /// <summary>Gets or sets how concentrated it is.</summary>
    public ConcentrationDTO Concentration { get; set; } = new();
}

/// <summary>One sale.</summary>
public sealed class RealizedSaleDTO
{
    /// <summary>Gets or sets the instrument.</summary>
    public InstrumentDTO Instrument { get; set; } = new();

    /// <summary>Gets or sets when (UTC).</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the shares sold.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the price, in its currency.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the price currency.</summary>
    public string PriceCurrency { get; set; } = string.Empty;

    /// <summary>Gets or sets what the sale brought in.</summary>
    public decimal Proceeds { get; set; }

    /// <summary>Gets or sets what the shares sold had cost (null = unknown).</summary>
    public decimal? Cost { get; set; }

    /// <summary>Gets or sets the gain (null = unknown).</summary>
    public decimal? Gain { get; set; }

    /// <summary>Gets or sets the gain against the cost.</summary>
    public decimal? GainPct { get; set; }
}

/// <summary>The realized gain of a year or of an instrument.</summary>
public sealed class RealizedGroupDTO
{
    /// <summary>Gets or sets the year (by year) or 0.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the instrument (by instrument).</summary>
    public InstrumentDTO? Instrument { get; set; }

    /// <summary>Gets or sets the gain.</summary>
    public decimal Gain { get; set; }

    /// <summary>Gets or sets the number of sales.</summary>
    public int Sales { get; set; }
}

/// <summary>The Realized screen: what the sales made, by year.</summary>
public sealed class RealizedDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the year shown.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the years with sales (and this one), newest first.</summary>
    public List<int> Years { get; set; } = [];

    /// <summary>Gets or sets the gain of the year.</summary>
    public decimal Gain { get; set; }

    /// <summary>Gets or sets what the year's sales brought in.</summary>
    public decimal Proceeds { get; set; }

    /// <summary>Gets or sets what the shares sold had cost.</summary>
    public decimal Cost { get; set; }

    /// <summary>Gets or sets the sales with a gain.</summary>
    public int Wins { get; set; }

    /// <summary>Gets or sets the sales with a loss.</summary>
    public int Losses { get; set; }

    /// <summary>Gets or sets the sales whose cost isn't known (bought before the history Obol has).</summary>
    public int Unknown { get; set; }

    /// <summary>Gets or sets the year's sales, newest first.</summary>
    public List<RealizedSaleDTO> Sales { get; set; } = [];

    /// <summary>Gets or sets the gain of every year, oldest first.</summary>
    public List<RealizedGroupDTO> ByYear { get; set; } = [];

    /// <summary>Gets or sets the year's gain by instrument, biggest first.</summary>
    public List<RealizedGroupDTO> ByInstrument { get; set; } = [];
}

/// <summary>A growth scenario of the projection.</summary>
public sealed class GoalScenarioDTO
{
    /// <summary>Gets or sets the key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the yearly return.</summary>
    public decimal Rate { get; set; }
}

/// <summary>The Goals screen: where the portfolio is now, for the projection the page works out.</summary>
public sealed class GoalsDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the net worth now.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets the money put in so far (null = transfers unknown).</summary>
    public decimal? Invested { get; set; }

    /// <summary>Gets or sets the money put in a month, on average over the last 12 months.</summary>
    public decimal MonthlyAverage { get; set; }

    /// <summary>Gets or sets the expected income of the next 12 months.</summary>
    public decimal IncomeNext12 { get; set; }

    /// <summary>Gets or sets the income yield on today's value.</summary>
    public decimal? Yield { get; set; }

    /// <summary>Gets or sets the money-weighted return a year so far.</summary>
    public decimal? YearlyReturn { get; set; }

    /// <summary>Gets or sets the scenarios.</summary>
    public List<GoalScenarioDTO> Scenarios { get; set; } = [];
}

/// <summary>A report of the AI coach.</summary>
public sealed class CoachReportDTO
{
    /// <summary>Gets or sets when it was written.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Gets or sets a one-line headline.</summary>
    public string Headline { get; set; } = string.Empty;

    /// <summary>Gets or sets a short summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets what's working.</summary>
    public List<string> Strengths { get; set; } = [];

    /// <summary>Gets or sets what to watch.</summary>
    public List<string> Watch { get; set; } = [];

    /// <summary>Gets or sets what to consider doing.</summary>
    public List<string> Actions { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether this is the demo's example report.</summary>
    public bool Example { get; set; }
}

/// <summary>The AI coach: set up or not, running, and the last report.</summary>
public sealed class CoachDTO
{
    /// <summary>Gets or sets a value indicating whether an AI provider is set up.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the provider.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Gets or sets the model.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a report is being written.</summary>
    public bool Running { get; set; }

    /// <summary>Gets or sets when the running report started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets the last error.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>Gets or sets the last report.</summary>
    public CoachReportDTO? Report { get; set; }
}
