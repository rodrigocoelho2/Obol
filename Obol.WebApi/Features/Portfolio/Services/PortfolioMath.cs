using Obol.Library.Enums;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Portfolio.Models.Entities;

namespace Obol.WebApi.Features.Portfolio.Services;

/// <summary>
/// Everything about one account the portfolio maths needs, loaded once per request.
/// </summary>
internal sealed class PortfolioData
{
    /// <summary>Gets the account currency.</summary>
    public required string Currency { get; init; }

    /// <summary>Gets today's date in the app's time zone.</summary>
    public required DateOnly Today { get; init; }

    /// <summary>Gets the instruments, by id.</summary>
    public required IReadOnlyDictionary<int, InstrumentRow> Instruments { get; init; }

    /// <summary>Gets the positions Trading 212 reported (empty without a sync).</summary>
    public required IReadOnlyList<HoldingRow> Holdings { get; init; }

    /// <summary>Gets a value indicating whether the positions come from Trading 212 (else they're worked out from the trades).</summary>
    public required bool BrokerHoldings { get; init; }

    /// <summary>Gets the trades, oldest first.</summary>
    public required IReadOnlyList<TradeRow> Trades { get; init; }

    /// <summary>Gets the dividends, oldest first.</summary>
    public required IReadOnlyList<DividendRow> Dividends { get; init; }

    /// <summary>Gets the cash movements, oldest first.</summary>
    public required IReadOnlyList<CashMovementRow> Cash { get; init; }

    /// <summary>Gets the daily snapshots, oldest first.</summary>
    public required IReadOnlyList<SnapshotRow> Snapshots { get; init; }

    /// <summary>Gets the prices and exchange rates.</summary>
    public required PriceBook Prices { get; init; }

    /// <summary>Gets the local day of a UTC time.</summary>
    public required Func<DateTime, DateOnly> LocalDay { get; init; }

    /// <summary>Gets the benchmark indexes: instrument key (BENCH:…) → instrument id.</summary>
    public IReadOnlyDictionary<string, int> Benchmarks { get; init; } = new Dictionary<string, int>();

    /// <summary>Gets the Trading 212 pies, as the last sync read them.</summary>
    public IReadOnlyList<PieRow> Pies { get; init; } = [];

    /// <summary>Gets the instruments of the pies.</summary>
    public IReadOnlyList<PieHoldingRow> PieHoldings { get; init; } = [];

    /// <summary>Gets a value indicating whether there is anything at all.</summary>
    public bool HasData => Holdings.Count > 0 || Trades.Count > 0 || Cash.Count > 0 || Dividends.Count > 0;
}

/// <summary>An open position, valued now.</summary>
internal sealed class Position
{
    /// <summary>Gets or sets the instrument.</summary>
    public required InstrumentRow Instrument { get; init; }

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

    /// <summary>Gets the unrealised gain.</summary>
    public decimal Unrealized => Value - Cost;

    /// <summary>Gets the unrealised gain as a share of the cost.</summary>
    public decimal? UnrealizedPct => Cost > 0 ? Unrealized / Cost : null;

    /// <summary>Gets or sets the change since the previous close, in the account currency.</summary>
    public decimal DayChange { get; set; }

    /// <summary>Gets or sets the change of the price since the previous close.</summary>
    public decimal? DayChangePct { get; set; }

    /// <summary>Gets or sets when it was opened (UTC).</summary>
    public DateTime? OpenedAt { get; set; }

    /// <summary>Gets or sets what the exchange rate added to or took from the value since bought (Trading 212's figure).</summary>
    public decimal? FxImpact { get; set; }
}

/// <summary>One day of the value chart.</summary>
/// <param name="Day">The day.</param>
/// <param name="Value">What everything was worth at the end of the day.</param>
/// <param name="Deposits">Money put in (minus taken out) up to that day.</param>
/// <param name="Cash">The cash part of the value.</param>
internal sealed record ValuePoint(DateOnly Day, decimal Value, decimal Deposits, decimal Cash = 0m);

/// <summary>
/// The portfolio maths: positions now, the value of every past day, today's change, the allocation. Pure functions
/// over <see cref="PortfolioData"/>, so the unit tests run them on fixed data.
/// </summary>
internal static class PortfolioMath
{
    private const decimal Dust = 0.000001m;

    /// <summary>
    /// The open positions now: as Trading 212 reported them, or worked out from the trades (average cost) when the
    /// account only has CSV imports.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <returns>The positions, biggest first.</returns>
    public static List<Position> Positions(PortfolioData data)
    {
        var positions = new List<Position>();
        if (data.BrokerHoldings)
        {
            foreach (var holding in data.Holdings)
            {
                if (!data.Instruments.TryGetValue(holding.InstrumentID, out var instrument) || holding.Quantity <= Dust)
                {
                    continue;
                }

                positions.Add(new Position
                {
                    Instrument = instrument,
                    Quantity = holding.Quantity,
                    AveragePrice = holding.AveragePrice,
                    Price = holding.CurrentPrice,
                    Value = holding.Value,
                    Cost = holding.Cost,
                    OpenedAt = holding.OpenedAt,
                    FxImpact = holding.FxImpact,
                });
            }
        }
        else
        {
            foreach (var (instrumentId, lot) in Lots(data.Trades))
            {
                if (lot.Quantity <= Dust || !data.Instruments.TryGetValue(instrumentId, out var instrument))
                {
                    continue;
                }

                var price = data.Prices.PriceOn(instrumentId, data.Today) ?? lot.AveragePrice;
                positions.Add(new Position
                {
                    Instrument = instrument,
                    Quantity = lot.Quantity,
                    AveragePrice = lot.AveragePrice,
                    Price = price,
                    Value = Math.Round(lot.Quantity * price * data.Prices.FxOn(instrument.Currency, data.Today), 2),
                    Cost = Math.Round(lot.Cost, 2),
                    OpenedAt = lot.OpenedAt,
                });
            }
        }

        foreach (var position in positions)
        {
            if (data.Prices.LastTwo(position.Instrument.InstrumentID, data.Today) is { } last && last.Day >= data.Today.AddDays(-6) && last.Previous > 0)
            {
                var currency = position.Instrument.Currency;
                position.DayChangePct = last.Price / last.Previous - 1m;
                position.DayChange = Math.Round(position.Quantity * (last.Price * data.Prices.FxOn(currency, last.Day) - last.Previous * data.Prices.FxOn(currency, last.PreviousDay)), 2);
            }
        }

        return positions.OrderByDescending(static position => position.Value).ToList();
    }

    /// <summary>
    /// The cash now: Trading 212's last snapshot, or (CSV only) everything that moved cash added up.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <returns>The cash, never below zero.</returns>
    public static decimal CashNow(PortfolioData data)
    {
        if (data.BrokerHoldings && data.Snapshots.Count > 0)
        {
            return data.Snapshots[^1].Cash;
        }

        var cash = data.Cash.Sum(static movement => movement.Amount)
            + data.Dividends.Sum(static dividend => dividend.Amount)
            + data.Trades.Sum(static trade => trade.IsBuy ? -trade.Total : trade.Total);
        return Math.Max(0m, Math.Round(cash, 2));
    }

    /// <summary>
    /// Money put in minus money taken out, or null when no deposit is known (the transfers weren't synced).
    /// </summary>
    /// <param name="data">The account.</param>
    /// <returns>The net deposits.</returns>
    public static decimal? NetDeposits(PortfolioData data)
    {
        var transfers = data.Cash.Where(static movement => movement.Kind is (byte)CashKind.Deposit or (byte)CashKind.Withdrawal).ToList();
        return transfers.Count == 0 ? null : transfers.Sum(static movement => movement.Amount);
    }

    /// <summary>
    /// The first day anything happened.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <returns>The day, or today.</returns>
    public static DateOnly FirstDay(PortfolioData data)
    {
        var days = new List<DateOnly>();
        if (data.Trades.Count > 0)
        {
            days.Add(data.LocalDay(data.Trades.Min(static trade => trade.ExecutedAt)));
        }

        if (data.Cash.Count > 0)
        {
            days.Add(data.LocalDay(data.Cash.Min(static movement => movement.At)));
        }

        if (data.Dividends.Count > 0)
        {
            days.Add(data.LocalDay(data.Dividends.Min(static dividend => dividend.PaidAt)));
        }

        if (data.Snapshots.Count > 0)
        {
            days.Add(DateOnly.FromDateTime(data.Snapshots[0].Day));
        }

        days.AddRange(data.Holdings.Where(static holding => holding.OpenedAt is not null).Select(holding => data.LocalDay(holding.OpenedAt!.Value)));
        return days.Count == 0 ? data.Today : days.Min();
    }

    /// <summary>
    /// The first day of a chart range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="today">Today.</param>
    /// <param name="firstDay">The first day anything happened.</param>
    /// <returns>The first day to draw.</returns>
    public static DateOnly RangeStart(ChartRange range, DateOnly today, DateOnly firstDay)
    {
        var start = range switch
        {
            ChartRange.Week => today.AddDays(-7),
            ChartRange.Month => today.AddMonths(-1),
            ChartRange.Quarter => today.AddMonths(-3),
            ChartRange.YearToDate => new DateOnly(today.Year, 1, 1),
            ChartRange.Year => today.AddYears(-1),
            _ => firstDay,
        };
        return start < firstDay ? firstDay : start > today ? today : start;
    }

    /// <summary>
    /// What everything was worth at the end of every day from <paramref name="from"/> to today. Starts from the positions
    /// and the cash of today and walks back through the trades and cash movements, valuing each day at that day's prices
    /// and exchange rates.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <param name="positions">The positions now.</param>
    /// <param name="cashNow">The cash now.</param>
    /// <param name="from">The first day.</param>
    /// <returns>One point per day, oldest first.</returns>
    public static List<ValuePoint> ValueSeries(PortfolioData data, IReadOnlyList<Position> positions, decimal cashNow, DateOnly from)
    {
        var quantities = positions.ToDictionary(static position => position.Instrument.InstrumentID, static position => position.Quantity);
        var cash = cashNow;

        var tradesByDay = data.Trades.GroupBy(trade => data.LocalDay(trade.ExecutedAt)).ToDictionary(static group => group.Key, static group => group.ToList());
        var cashByDay = new Dictionary<DateOnly, decimal>();
        void AddCash(DateOnly day, decimal amount) => cashByDay[day] = cashByDay.GetValueOrDefault(day) + amount;
        foreach (var movement in data.Cash)
        {
            AddCash(data.LocalDay(movement.At), movement.Amount);
        }

        foreach (var dividend in data.Dividends)
        {
            AddCash(data.LocalDay(dividend.PaidAt), dividend.Amount);
        }

        foreach (var trade in data.Trades)
        {
            AddCash(data.LocalDay(trade.ExecutedAt), trade.IsBuy ? -trade.Total : trade.Total);
        }

        // Deposits run forwards: the sum of every transfer up to each day.
        var transfers = data.Cash.Where(static movement => movement.Kind is (byte)CashKind.Deposit or (byte)CashKind.Withdrawal)
            .GroupBy(movement => data.LocalDay(movement.At))
            .ToDictionary(static group => group.Key, static group => group.Sum(static movement => movement.Amount));
        var depositsBefore = transfers.Where(pair => pair.Key < from).Sum(static pair => pair.Value);

        var points = new List<ValuePoint>();
        for (var day = data.Today; day >= from; day = day.AddDays(-1))
        {
            var value = Math.Max(0m, cash);
            foreach (var (instrumentId, quantity) in quantities)
            {
                if (quantity <= Dust || !data.Instruments.TryGetValue(instrumentId, out var instrument))
                {
                    continue;
                }

                if (data.Prices.PriceOn(instrumentId, day) is { } price)
                {
                    value += quantity * price * data.Prices.FxOn(instrument.Currency, day);
                }
            }

            points.Add(new ValuePoint(day, Math.Round(value, 2), 0m, Math.Round(Math.Max(0m, cash), 2)));

            // Undo the day: the state at the end of the day before.
            if (tradesByDay.TryGetValue(day, out var trades))
            {
                foreach (var trade in trades)
                {
                    quantities[trade.InstrumentID] = quantities.GetValueOrDefault(trade.InstrumentID) + (trade.IsBuy ? -trade.Quantity : trade.Quantity);
                }
            }

            cash -= cashByDay.GetValueOrDefault(day);
        }

        points.Reverse();
        var deposits = depositsBefore;
        for (var i = 0; i < points.Count; i++)
        {
            deposits += transfers.GetValueOrDefault(points[i].Day);
            points[i] = points[i] with { Deposits = deposits };
        }

        return points;
    }

    /// <summary>
    /// The gain over a chart (what the value moved, minus the money put in meanwhile) and its share of what was there.
    /// </summary>
    /// <param name="points">The chart.</param>
    /// <returns>(gain, share), or null for an empty chart.</returns>
    public static (decimal Change, decimal? Pct)? RangeChange(IReadOnlyList<ValuePoint> points)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var first = points[0];
        var last = points[^1];
        var flows = last.Deposits - first.Deposits;
        var change = last.Value - first.Value - flows;
        var basis = first.Value + Math.Max(0m, flows);
        return (Math.Round(change, 2), basis > 0 ? change / basis : null);
    }

    /// <summary>
    /// Thins a long chart to about <paramref name="max"/> points (always keeping the first and the last).
    /// </summary>
    /// <typeparam name="T">The point type.</typeparam>
    /// <param name="points">The points.</param>
    /// <param name="max">The most points wanted.</param>
    /// <returns>The thinned points.</returns>
    public static List<T> Thin<T>(IReadOnlyList<T> points, int max = 240)
    {
        if (points.Count <= max)
        {
            return points.ToList();
        }

        var step = (double)(points.Count - 1) / (max - 1);
        var result = new List<T>(max);
        for (var i = 0; i < max - 1; i++)
        {
            result.Add(points[(int)Math.Round(i * step)]);
        }

        result.Add(points[^1]);
        return result;
    }

    /// <summary>
    /// The label of an asset class on the screens.
    /// </summary>
    /// <param name="assetClass">The class.</param>
    /// <returns>The label.</returns>
    public static string Label(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Stock => "Stocks",
        AssetClass.EquityEtf => "Equity ETFs",
        AssetClass.BondEtf => "Bond ETFs",
        AssetClass.Crypto => "Crypto",
        AssetClass.Commodity => "Commodities",
        _ => "Other",
    };

    /// <summary>
    /// The shares held of every instrument after the trades (average cost: a sell takes its share of the cost).
    /// </summary>
    /// <param name="trades">The trades, oldest first.</param>
    /// <returns>The lots, by instrument.</returns>
    public static Dictionary<int, Lot> Lots(IEnumerable<TradeRow> trades)
    {
        var lots = new Dictionary<int, Lot>();
        foreach (var trade in trades.OrderBy(static trade => trade.ExecutedAt))
        {
            if (!lots.TryGetValue(trade.InstrumentID, out var lot))
            {
                lot = new Lot();
                lots[trade.InstrumentID] = lot;
            }

            if (trade.IsBuy)
            {
                if (lot.Quantity <= Dust)
                {
                    lot.OpenedAt = trade.ExecutedAt;
                }

                var quantity = lot.Quantity + trade.Quantity;
                lot.AveragePrice = quantity > 0 ? (lot.AveragePrice * lot.Quantity + trade.Price * trade.Quantity) / quantity : 0m;
                lot.Quantity = quantity;
                lot.Cost += trade.Total;
            }
            else
            {
                var share = lot.Quantity > 0 ? Math.Min(1m, trade.Quantity / lot.Quantity) : 1m;
                lot.Cost -= lot.Cost * share;
                lot.Quantity = Math.Max(0m, lot.Quantity - trade.Quantity);
                if (lot.Quantity <= Dust)
                {
                    lot.Quantity = 0m;
                    lot.Cost = 0m;
                    lot.AveragePrice = 0m;
                    lot.OpenedAt = null;
                }
            }
        }

        return lots;
    }

    /// <summary>
    /// The shares of an instrument held at the end of a day, from the trades.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <param name="instrumentId">The instrument.</param>
    /// <param name="day">The day.</param>
    /// <returns>The shares.</returns>
    public static decimal QuantityOn(PortfolioData data, int instrumentId, DateOnly day) =>
        Math.Max(0m, data.Trades.Where(trade => trade.InstrumentID == instrumentId && data.LocalDay(trade.ExecutedAt) <= day)
            .Sum(static trade => trade.IsBuy ? trade.Quantity : -trade.Quantity));

    /// <summary>Shares held and what they cost, while walking the trades.</summary>
    internal sealed class Lot
    {
        /// <summary>Gets or sets the shares.</summary>
        public decimal Quantity { get; set; }

        /// <summary>Gets or sets the average price paid, in the instrument currency.</summary>
        public decimal AveragePrice { get; set; }

        /// <summary>Gets or sets what the shares held cost, in the account currency.</summary>
        public decimal Cost { get; set; }

        /// <summary>Gets or sets when the position was opened.</summary>
        public DateTime? OpenedAt { get; set; }
    }
}
