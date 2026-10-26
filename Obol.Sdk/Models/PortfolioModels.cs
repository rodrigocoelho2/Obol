using Obol.Library.Enums;

namespace Obol.Sdk.Models;

/// <summary>An instrument as the screens show it.</summary>
public sealed class InstrumentDTO
{
    /// <summary>Gets or sets the key (ISIN, or T212:ticker): what links and logos use.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISIN.</summary>
    public string? Isin { get; set; }

    /// <summary>Gets or sets the symbol (AAPL).</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency prices are in.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the asset class.</summary>
    public AssetClass Class { get; set; }

    /// <summary>Gets or sets the Trading 212 ticker.</summary>
    public string Ticker { get; set; } = string.Empty;
}

/// <summary>An open position.</summary>
public sealed class PositionDTO
{
    /// <summary>Gets or sets the instrument.</summary>
    public InstrumentDTO Instrument { get; set; } = new();

    /// <summary>Gets or sets the shares.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the average price paid, in the instrument currency.</summary>
    public decimal AveragePrice { get; set; }

    /// <summary>Gets or sets the price now, in the instrument currency.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the value, in the account currency.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets what it cost, in the account currency.</summary>
    public decimal Cost { get; set; }

    /// <summary>Gets or sets the unrealised gain.</summary>
    public decimal Unrealized { get; set; }

    /// <summary>Gets or sets the unrealised gain as a share of the cost (0.183 = 18.3 %).</summary>
    public decimal? UnrealizedPct { get; set; }

    /// <summary>Gets or sets the share of all positions (0.378 = 37.8 %).</summary>
    public decimal Weight { get; set; }

    /// <summary>Gets or sets the change since the previous close, in the account currency.</summary>
    public decimal DayChange { get; set; }

    /// <summary>Gets or sets the price change since the previous close (0.0058 = 0.58 %).</summary>
    public decimal? DayChangePct { get; set; }

    /// <summary>Gets or sets the last 30 closes, for a sparkline.</summary>
    public List<decimal> Spark { get; set; } = [];

    /// <summary>Gets or sets when the position was opened.</summary>
    public DateTimeOffset? OpenedAt { get; set; }
}

/// <summary>One day of a value chart.</summary>
public sealed class ValuePointDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Day { get; set; }

    /// <summary>Gets or sets the value at the end of the day.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets the net deposits up to that day.</summary>
    public decimal Deposits { get; set; }
}

/// <summary>A slice of the allocation.</summary>
public sealed class AllocationSliceDTO
{
    /// <summary>Gets or sets the key: an <see cref="AssetClass"/> name or "Cash".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the value.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets the share of the total (0.556 = 55.6 %).</summary>
    public decimal Percent { get; set; }
}

/// <summary>A dividend expected from the last twelve months' payments.</summary>
public sealed class UpcomingPaymentDTO
{
    /// <summary>Gets or sets the instrument.</summary>
    public InstrumentDTO Instrument { get; set; } = new();

    /// <summary>Gets or sets the expected day (a year after the same payment last year).</summary>
    public DateOnly Day { get; set; }

    /// <summary>Gets or sets the expected amount, in the account currency.</summary>
    public decimal Amount { get; set; }
}

/// <summary>The Overview screen.</summary>
public sealed class OverviewDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets a value indicating whether there is anything to show.</summary>
    public bool HasData { get; set; }

    /// <summary>Gets or sets a value indicating whether Trading 212 is connected.</summary>
    public bool Connected { get; set; }

    /// <summary>Gets or sets when Trading 212 last synced.</summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>Gets or sets the value of everything (positions and cash).</summary>
    public decimal TotalValue { get; set; }

    /// <summary>Gets or sets the cash.</summary>
    public decimal Cash { get; set; }

    /// <summary>Gets or sets what the open positions cost.</summary>
    public decimal Cost { get; set; }

    /// <summary>Gets or sets the unrealised gain.</summary>
    public decimal Unrealized { get; set; }

    /// <summary>Gets or sets the unrealised gain as a share of the cost.</summary>
    public decimal? UnrealizedPct { get; set; }

    /// <summary>Gets or sets money put in minus taken out (null when no transfer is known).</summary>
    public decimal? NetDeposits { get; set; }

    /// <summary>Gets or sets the total return: the value minus the net deposits.</summary>
    public decimal? TotalReturn { get; set; }

    /// <summary>Gets or sets the total return as a share of the net deposits.</summary>
    public decimal? TotalReturnPct { get; set; }

    /// <summary>Gets or sets the change since the previous close.</summary>
    public decimal DayChange { get; set; }

    /// <summary>Gets or sets the change since the previous close, as a share.</summary>
    public decimal? DayChangePct { get; set; }

    /// <summary>Gets or sets the range of the chart.</summary>
    public ChartRange Range { get; set; }

    /// <summary>Gets or sets the gain over the range (deposits taken out).</summary>
    public decimal? RangeChange { get; set; }

    /// <summary>Gets or sets the gain over the range, as a share.</summary>
    public decimal? RangeChangePct { get; set; }

    /// <summary>Gets or sets the gain over the last twelve months.</summary>
    public decimal? YearChangePct { get; set; }

    /// <summary>Gets or sets how far ahead (or behind) of MSCI World the same deposits are over the range (null without its prices).</summary>
    public decimal? VsWorld { get; set; }

    /// <summary>Gets or sets the chart.</summary>
    public List<ValuePointDTO> Series { get; set; } = [];

    /// <summary>Gets or sets the allocation by asset class (and cash).</summary>
    public List<AllocationSliceDTO> Allocation { get; set; } = [];

    /// <summary>Gets or sets the biggest movers today.</summary>
    public List<PositionDTO> Movers { get; set; } = [];

    /// <summary>Gets or sets every open position (biggest first).</summary>
    public List<PositionDTO> Positions { get; set; } = [];

    /// <summary>Gets or sets the Trading 212 pies (biggest first; empty when there are none or they weren't read).</summary>
    public List<PieDTO> Pies { get; set; } = [];

    /// <summary>Gets or sets the shares held outside every pie, one line per instrument (biggest first).</summary>
    public List<PieHoldingDTO> OutsidePies { get; set; } = [];

    /// <summary>Gets or sets the dividends and interest received this year.</summary>
    public decimal IncomeThisYear { get; set; }

    /// <summary>Gets or sets the dividends expected over the next twelve months.</summary>
    public decimal IncomeNext12 { get; set; }

    /// <summary>Gets or sets the share of this year's expected income already received.</summary>
    public decimal? IncomeProgress { get; set; }

    /// <summary>Gets or sets the next expected payments.</summary>
    public List<UpcomingPaymentDTO> Upcoming { get; set; } = [];
}

/// <summary>The Holdings screen.</summary>
public sealed class HoldingsDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the value of the positions.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets what they cost.</summary>
    public decimal Cost { get; set; }

    /// <summary>Gets or sets the unrealised gain.</summary>
    public decimal Unrealized { get; set; }

    /// <summary>Gets or sets the unrealised gain as a share of the cost.</summary>
    public decimal? UnrealizedPct { get; set; }

    /// <summary>Gets or sets the cash.</summary>
    public decimal Cash { get; set; }

    /// <summary>Gets or sets the positions, biggest first.</summary>
    public List<PositionDTO> Positions { get; set; } = [];
}

/// <summary>One price of a position chart.</summary>
public sealed class PricePointDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Day { get; set; }

    /// <summary>Gets or sets the close, in the instrument currency.</summary>
    public decimal Close { get; set; }
}

/// <summary>A buy or a sell marked on a position chart.</summary>
public sealed class TradeMarkDTO
{
    /// <summary>Gets or sets the day.</summary>
    public DateOnly Day { get; set; }

    /// <summary>Gets or sets a value indicating whether it was a buy.</summary>
    public bool Buy { get; set; }

    /// <summary>Gets or sets the shares.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }
}

/// <summary>A line of the activity.</summary>
public sealed class ActivityItemDTO
{
    /// <summary>Gets or sets the kind.</summary>
    public ActivityKind Kind { get; set; }

    /// <summary>Gets or sets when.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the instrument (trades and dividends).</summary>
    public InstrumentDTO? Instrument { get; set; }

    /// <summary>Gets or sets the shares.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Gets or sets the price per share.</summary>
    public decimal? Price { get; set; }

    /// <summary>Gets or sets the currency of the price.</summary>
    public string? PriceCurrency { get; set; }

    /// <summary>Gets or sets the amount in the account currency: positive in, negative out.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets a note.</summary>
    public string? Note { get; set; }

    /// <summary>Gets or sets where it came from.</summary>
    public DataSource Source { get; set; }
}

/// <summary>The detail of one position (or of an instrument held before).</summary>
public sealed class PositionDetailDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the instrument.</summary>
    public InstrumentDTO Instrument { get; set; } = new();

    /// <summary>Gets or sets the open position (null when it was sold).</summary>
    public PositionDTO? Position { get; set; }

    /// <summary>Gets or sets the range of the chart.</summary>
    public ChartRange Range { get; set; }

    /// <summary>Gets or sets the prices of the range.</summary>
    public List<PricePointDTO> Prices { get; set; } = [];

    /// <summary>Gets or sets the price change over the range.</summary>
    public decimal? RangeChangePct { get; set; }

    /// <summary>Gets or sets the buys and sells of the range.</summary>
    public List<TradeMarkDTO> Marks { get; set; } = [];

    /// <summary>Gets or sets every dividend it paid, added up.</summary>
    public decimal Dividends { get; set; }

    /// <summary>Gets or sets every realised gain of its sells, added up.</summary>
    public decimal Realized { get; set; }

    /// <summary>Gets or sets what was put into it, added up.</summary>
    public decimal Invested { get; set; }

    /// <summary>Gets or sets its trades and dividends, newest first.</summary>
    public List<ActivityItemDTO> Activity { get; set; } = [];
}

/// <summary>A page of the activity.</summary>
public sealed class ActivityPageDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the lines, newest first.</summary>
    public List<ActivityItemDTO> Items { get; set; } = [];

    /// <summary>Gets or sets how many lines there are in all.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the lines skipped.</summary>
    public int Skip { get; set; }

    /// <summary>Gets or sets the lines asked for.</summary>
    public int Take { get; set; }
}

/// <summary>One month of income.</summary>
public sealed class IncomeMonthDTO
{
    /// <summary>Gets or sets the month (1–12).</summary>
    public int Month { get; set; }

    /// <summary>Gets or sets what was received.</summary>
    public decimal Received { get; set; }

    /// <summary>Gets or sets what is still expected (future months only).</summary>
    public decimal Expected { get; set; }

    /// <summary>Gets or sets a value indicating whether the month hasn't ended yet.</summary>
    public bool IsFuture { get; set; }
}

/// <summary>Where income came from.</summary>
public sealed class IncomeSourceDTO
{
    /// <summary>Gets or sets the instrument (null for interest and "other").</summary>
    public InstrumentDTO? Instrument { get; set; }

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the share of the year's income.</summary>
    public decimal Percent { get; set; }
}

/// <summary>The income of one instrument over the last 12 months.</summary>
public sealed class IncomePositionDTO
{
    /// <summary>Gets or sets the instrument.</summary>
    public InstrumentDTO Instrument { get; set; } = new();

    /// <summary>Gets or sets what's held of it now.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets what it paid in the last 12 months.</summary>
    public decimal Received { get; set; }

    /// <summary>Gets or sets the yield: the last 12 months' income on today's value.</summary>
    public decimal? Yield { get; set; }

    /// <summary>Gets or sets the yield on what was paid for it.</summary>
    public decimal? YieldOnCost { get; set; }

    /// <summary>Gets or sets the payments in the last 12 months.</summary>
    public int Payments { get; set; }

    /// <summary>Gets or sets the last payment per share, in the instrument currency.</summary>
    public decimal? LastPerShare { get; set; }

    /// <summary>Gets or sets the change of the last payment per share against the one before.</summary>
    public decimal? Change { get; set; }

    /// <summary>Gets or sets raised, cut, steady or empty.</summary>
    public string Trend { get; set; } = string.Empty;

    /// <summary>Gets or sets the tax withheld at source in the last 12 months.</summary>
    public decimal Withheld { get; set; }
}

/// <summary>The Income screen.</summary>
public sealed class IncomeDTO
{
    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets the year shown.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the years with income (and this one).</summary>
    public List<int> Years { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the year shown is this year.</summary>
    public bool IsCurrentYear { get; set; }

    /// <summary>Gets or sets the dividends and interest received in the year.</summary>
    public decimal Received { get; set; }

    /// <summary>Gets or sets the interest part of it.</summary>
    public decimal Interest { get; set; }

    /// <summary>Gets or sets what the whole year should bring (received plus still expected).</summary>
    public decimal ExpectedYear { get; set; }

    /// <summary>Gets or sets the share of the year's expected income already received.</summary>
    public decimal? Progress { get; set; }

    /// <summary>Gets or sets the dividends expected over the next twelve months.</summary>
    public decimal Next12 { get; set; }

    /// <summary>Gets or sets the next twelve months' dividends as a share of what the positions cost.</summary>
    public decimal? YieldOnCost { get; set; }

    /// <summary>Gets or sets the months.</summary>
    public List<IncomeMonthDTO> Months { get; set; } = [];

    /// <summary>Gets or sets the next expected payments.</summary>
    public List<UpcomingPaymentDTO> Upcoming { get; set; } = [];

    /// <summary>Gets or sets where the year's income came from.</summary>
    public List<IncomeSourceDTO> Sources { get; set; } = [];

    /// <summary>Gets or sets every payment received in the year, newest first.</summary>
    public List<ActivityItemDTO> Payments { get; set; } = [];

    /// <summary>Gets or sets the income by instrument (last 12 months).</summary>
    public List<IncomePositionDTO> Positions { get; set; } = [];

    /// <summary>Gets or sets the tax withheld at source in the last 12 months.</summary>
    public decimal Withheld { get; set; }
}

/// <summary>A Trading 212 pie: its instruments valued at today's prices, against what was put in.</summary>
public sealed class PieDTO
{
    /// <summary>Gets or sets the Trading 212 id.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the Trading 212 icon name.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Gets or sets what the pie's shares are worth, in the account currency.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets what was put into them.</summary>
    public decimal Invested { get; set; }

    /// <summary>Gets or sets the gain (value minus invested).</summary>
    public decimal Gain { get; set; }

    /// <summary>Gets or sets the gain as a share of what was put in (0.183 = 18.3 %).</summary>
    public decimal? GainPct { get; set; }

    /// <summary>Gets or sets the share of all positions.</summary>
    public decimal Weight { get; set; }

    /// <summary>Gets or sets the cash waiting in the pie.</summary>
    public decimal Cash { get; set; }

    /// <summary>Gets or sets the dividends the pie has received.</summary>
    public decimal DividendsGained { get; set; }

    /// <summary>Gets or sets the goal set for the pie.</summary>
    public decimal? Goal { get; set; }

    /// <summary>Gets or sets the instruments (biggest first).</summary>
    public List<PieHoldingDTO> Holdings { get; set; } = [];
}

/// <summary>Shares of one instrument inside a pie, or outside every pie.</summary>
public sealed class PieHoldingDTO
{
    /// <summary>Gets or sets the instrument.</summary>
    public InstrumentDTO Instrument { get; set; } = new();

    /// <summary>Gets or sets the shares.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets what they are worth, in the account currency.</summary>
    public decimal Value { get; set; }

    /// <summary>Gets or sets what they cost.</summary>
    public decimal Invested { get; set; }

    /// <summary>Gets or sets the gain.</summary>
    public decimal Gain { get; set; }

    /// <summary>Gets or sets the gain as a share of the cost.</summary>
    public decimal? GainPct { get; set; }

    /// <summary>Gets or sets the share of all positions.</summary>
    public decimal Weight { get; set; }

    /// <summary>Gets or sets the share of the pie now (null outside pies).</summary>
    public decimal? CurrentShare { get; set; }

    /// <summary>Gets or sets the share of the pie it was set to (null outside pies).</summary>
    public decimal? ExpectedShare { get; set; }
}
