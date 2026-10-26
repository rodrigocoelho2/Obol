using Obol.Library.Enums;
using Obol.WebApi.Features.Portfolio.Models.Entities;
using Obol.WebApi.Features.Portfolio.Services;

namespace Obol.WebApi.Features.Insights.Services;

/// <summary>Money moved into (positive) or out of (negative) the portfolio on a day.</summary>
/// <param name="Day">The day.</param>
/// <param name="Amount">The amount, in the account currency.</param>
internal sealed record CashFlow(DateOnly Day, decimal Amount);

/// <summary>One sale with what it made.</summary>
/// <param name="Trade">The sell.</param>
/// <param name="Cost">What the shares sold had cost (average cost), or null when the buys aren't known.</param>
/// <param name="Gain">The gain (Trading 212's own figure when it gave one), or null.</param>
internal sealed record Sale(TradeRow Trade, decimal? Cost, decimal? Gain);

/// <summary>
/// The maths of the Insights screens, as pure functions: money-weighted (XIRR) and time-weighted (TWR) returns, the
/// same money put in an index, monthly returns, realized gains and the ISIN countries.
/// </summary>
internal static class InsightsMath
{
    private const decimal MaxDailyMove = 0.5m;

    /// <summary>
    /// The money that came into the portfolio, day by day. Deposits and withdrawals when they're known (the value then
    /// includes the cash); otherwise the trades themselves (a buy puts money in, a sale takes it out) and the value
    /// is only what's invested.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <returns>The flows by day, and whether they are the transfers.</returns>
    public static (IReadOnlyDictionary<DateOnly, decimal> Flows, bool FromTransfers) Flows(PortfolioData data)
    {
        var transfers = data.Cash.Where(static movement => movement.Kind is (byte)CashKind.Deposit or (byte)CashKind.Withdrawal).ToList();
        if (transfers.Count > 0)
        {
            return (transfers.GroupBy(movement => data.LocalDay(movement.At)).ToDictionary(static group => group.Key, static group => group.Sum(static movement => movement.Amount)), true);
        }

        return (data.Trades.GroupBy(trade => data.LocalDay(trade.ExecutedAt))
            .ToDictionary(static group => group.Key, static group => group.Sum(static trade => trade.IsBuy ? trade.Total : -trade.Total)), false);
    }

    /// <summary>
    /// The value the returns are measured on: everything (positions and cash) when the transfers are known, else only
    /// what's invested.
    /// </summary>
    /// <param name="point">A day of the value chart.</param>
    /// <param name="fromTransfers">Whether the flows are the transfers.</param>
    /// <returns>The value.</returns>
    public static decimal Measured(ValuePoint point, bool fromTransfers) => fromTransfers ? point.Value : Math.Max(0m, point.Value - point.Cash);

    /// <summary>
    /// The time-weighted return: each day's move with that day's deposits and withdrawals taken out, chained. It's how
    /// funds and indexes report, so it's the fair comparison with a benchmark.
    /// </summary>
    /// <param name="values">The value at the end of each day, oldest first.</param>
    /// <param name="flows">The money moved in on each day.</param>
    /// <returns>The growth of 1 invested on the first day, for every day (1 = flat).</returns>
    public static List<(DateOnly Day, decimal Index)> TimeWeighted(IReadOnlyList<(DateOnly Day, decimal Value)> values, IReadOnlyDictionary<DateOnly, decimal> flows)
    {
        var index = new List<(DateOnly Day, decimal Index)>(values.Count);
        var growth = 1m;
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0 && values[i - 1].Value > 0)
            {
                var move = (values[i].Value - flows.GetValueOrDefault(values[i].Day)) / values[i - 1].Value - 1m;
                growth *= 1m + Math.Clamp(move, -MaxDailyMove, MaxDailyMove);
            }

            index.Add((values[i].Day, growth));
        }

        return index;
    }

    /// <summary>
    /// The money-weighted return a year (XIRR): the one rate that makes what was put in, when it was put in, grow to
    /// what's there now. It's "how did my money do", deposits' timing included.
    /// </summary>
    /// <param name="flows">The money put in (positive) or taken out (negative), by day, the first day's value included.</param>
    /// <param name="end">The last day.</param>
    /// <param name="endValue">The value on the last day.</param>
    /// <returns>The yearly rate, or null when it can't be worked out (nothing put in, or no change of sign).</returns>
    public static decimal? Xirr(IReadOnlyList<CashFlow> flows, DateOnly end, decimal endValue)
    {
        var items = flows.Where(static flow => flow.Amount != 0).Select(flow => (Years: 0.0, Amount: -(double)flow.Amount, flow.Day)).ToList();
        if (items.Count == 0 || endValue < 0 || !items.Any(static item => item.Amount < 0))
        {
            return null;
        }

        var first = items.Min(static item => item.Day);
        if (end <= first)
        {
            return null;
        }

        var all = items.Select(item => (Years: (item.Day.DayNumber - first.DayNumber) / 365.0, item.Amount)).ToList();
        all.Add((Years: (end.DayNumber - first.DayNumber) / 365.0, Amount: (double)endValue));
        if (!all.Any(static item => item.Amount > 0))
        {
            return null;
        }

        double Npv(double rate) => all.Sum(item => item.Amount / Math.Pow(1 + rate, item.Years));
        double Slope(double rate) => all.Sum(item => -item.Years * item.Amount / Math.Pow(1 + rate, item.Years + 1));

        // Newton from 10 %, then bisection if it wanders off.
        var guess = 0.1;
        for (var step = 0; step < 50; step++)
        {
            var value = Npv(guess);
            var slope = Slope(guess);
            if (Math.Abs(value) < 1e-7)
            {
                return Valid(guess);
            }

            if (slope == 0 || double.IsNaN(slope))
            {
                break;
            }

            var next = guess - value / slope;
            if (next <= -0.9999 || double.IsNaN(next) || double.IsInfinity(next))
            {
                break;
            }

            if (Math.Abs(next - guess) < 1e-9)
            {
                return Valid(next);
            }

            guess = next;
        }

        double low = -0.9999, high = 100;
        double lowValue = Npv(low), highValue = Npv(high);
        if (double.IsNaN(lowValue) || double.IsNaN(highValue) || Math.Sign(lowValue) == Math.Sign(highValue))
        {
            return null;
        }

        for (var step = 0; step < 200; step++)
        {
            var middle = (low + high) / 2;
            var value = Npv(middle);
            if (Math.Abs(value) < 1e-7 || high - low < 1e-10)
            {
                return Valid(middle);
            }

            if (Math.Sign(value) == Math.Sign(lowValue))
            {
                low = middle;
                lowValue = value;
            }
            else
            {
                high = middle;
            }
        }

        return Valid((low + high) / 2);

        static decimal? Valid(double rate) => double.IsFinite(rate) && rate > -1 && rate < 100 ? Math.Round((decimal)rate, 6) : null;
    }

    /// <summary>
    /// What the same money would be worth in an index: the first day's value and every later deposit buy units of it
    /// at that day's close, every withdrawal sells some.
    /// </summary>
    /// <param name="days">The days, oldest first.</param>
    /// <param name="startValue">The value on the first day.</param>
    /// <param name="flows">The money moved in on each day.</param>
    /// <param name="price">The index's close on a day, in the account currency (null = unknown).</param>
    /// <returns>The value on every day, or null when the index has no price.</returns>
    public static List<decimal>? SameMoneyIn(IReadOnlyList<DateOnly> days, decimal startValue, IReadOnlyDictionary<DateOnly, decimal> flows, Func<DateOnly, decimal?> price)
    {
        if (days.Count == 0 || price(days[0]) is not { } first || first <= 0)
        {
            return null;
        }

        var units = startValue / first;
        var values = new List<decimal>(days.Count) { Math.Round(startValue, 2) };
        for (var i = 1; i < days.Count; i++)
        {
            var close = price(days[i]) ?? 0m;
            if (close <= 0)
            {
                values.Add(values[^1]);
                continue;
            }

            units = Math.Max(0m, units + flows.GetValueOrDefault(days[i]) / close);
            values.Add(Math.Round(units * close, 2));
        }

        return values;
    }

    /// <summary>
    /// Monthly returns from a time-weighted index: the move from the end of one month to the end of the next.
    /// </summary>
    /// <param name="index">The growth of 1 by day.</param>
    /// <returns>(year, month, return), oldest first; the first month is measured from the first day.</returns>
    public static List<(int Year, int Month, decimal Return)> Monthly(IReadOnlyList<(DateOnly Day, decimal Index)> index)
    {
        var result = new List<(int, int, decimal)>();
        if (index.Count < 2)
        {
            return result;
        }

        var previous = index[0].Index;
        foreach (var month in index.GroupBy(static point => (point.Day.Year, point.Day.Month)))
        {
            var last = month.Last().Index;
            if (previous > 0)
            {
                result.Add((month.Key.Year, month.Key.Month, Math.Round(last / previous - 1m, 6)));
            }

            previous = last;
        }

        return result;
    }

    /// <summary>
    /// Every sale with what it made: Trading 212's realised result when it gave one, else the proceeds against the
    /// average cost of the shares sold (from the buys before it).
    /// </summary>
    /// <param name="trades">The trades, oldest first.</param>
    /// <returns>The sales, oldest first.</returns>
    public static List<Sale> Sales(IEnumerable<TradeRow> trades)
    {
        var lots = new Dictionary<int, (decimal Quantity, decimal Cost)>();
        var sales = new List<Sale>();
        foreach (var trade in trades.OrderBy(static trade => trade.ExecutedAt))
        {
            var lot = lots.GetValueOrDefault(trade.InstrumentID);
            if (trade.IsBuy)
            {
                lots[trade.InstrumentID] = (lot.Quantity + trade.Quantity, lot.Cost + trade.Total);
                continue;
            }

            decimal? cost = null;
            if (lot.Quantity > 0)
            {
                var share = Math.Min(1m, trade.Quantity / lot.Quantity);
                cost = Math.Round(lot.Cost * share, 2);
                lots[trade.InstrumentID] = (Math.Max(0m, lot.Quantity - trade.Quantity), lot.Cost - lot.Cost * share);
            }

            // Shares bought before the history Obol has: Trading 212's result tells the cost.
            if (trade.RealizedPL is { } result && (cost is null || lot.Quantity < trade.Quantity))
            {
                cost = Math.Round(trade.Total - result, 2);
            }

            var gain = trade.RealizedPL ?? (cost is { } known ? Math.Round(trade.Total - known, 2) : null);
            sales.Add(new Sale(trade, cost, gain));
        }

        return sales;
    }

    /// <summary>The country an ISIN was issued in (its first two letters), as a name.</summary>
    /// <param name="isin">The ISIN.</param>
    /// <returns>(code, name); ("", "Unknown") without an ISIN.</returns>
    public static (string Code, string Name) Country(string? isin)
    {
        if (isin is not { Length: 12 })
        {
            return (string.Empty, "Unknown");
        }

        var code = isin[..2].ToUpperInvariant();
        return (code, Countries.TryGetValue(code, out var name) ? name : code);
    }

    private static readonly Dictionary<string, string> Countries = new(StringComparer.Ordinal)
    {
        ["US"] = "United States", ["CA"] = "Canada", ["GB"] = "United Kingdom", ["IE"] = "Ireland", ["LU"] = "Luxembourg",
        ["DE"] = "Germany", ["FR"] = "France", ["NL"] = "Netherlands", ["PT"] = "Portugal", ["ES"] = "Spain", ["IT"] = "Italy",
        ["BE"] = "Belgium", ["CH"] = "Switzerland", ["AT"] = "Austria", ["DK"] = "Denmark", ["SE"] = "Sweden", ["NO"] = "Norway",
        ["FI"] = "Finland", ["JP"] = "Japan", ["CN"] = "China", ["HK"] = "Hong Kong", ["TW"] = "Taiwan", ["KR"] = "South Korea",
        ["IN"] = "India", ["AU"] = "Australia", ["BR"] = "Brazil", ["MX"] = "Mexico", ["IL"] = "Israel", ["SG"] = "Singapore",
        ["KY"] = "Cayman Islands", ["BM"] = "Bermuda", ["MH"] = "Marshall Islands", ["JE"] = "Jersey", ["GG"] = "Guernsey",
        ["CY"] = "Cyprus", ["GR"] = "Greece", ["PL"] = "Poland", ["ZA"] = "South Africa", ["AR"] = "Argentina", ["CL"] = "Chile",
        ["PA"] = "Panama", ["UY"] = "Uruguay", ["LR"] = "Liberia", ["VG"] = "British Virgin Islands", ["CW"] = "Curaçao",
    };
}
