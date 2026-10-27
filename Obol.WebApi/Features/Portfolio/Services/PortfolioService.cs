using Obol.Library.Enums;
using Obol.Library.Services;
using Obol.WebApi.Extensions.Globalization;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Portfolio.Models.DTOs;
using Obol.WebApi.Features.Portfolio.Models.Entities;

namespace Obol.WebApi.Features.Portfolio.Services;

/// <summary>
/// The screens: Overview, Holdings, a position, the activity and Income.
/// </summary>
internal interface IPortfolioService
{
    /// <summary>The Overview: value, chart, allocation, movers and income.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="range">The chart range.</param>
    /// <returns>The overview.</returns>
    Task<ServiceApiResponse<OverviewDTO>> OverviewAsync(string userId, ChartRange range);

    /// <summary>The open positions.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The holdings.</returns>
    Task<ServiceApiResponse<HoldingsDTO>> HoldingsAsync(string userId);

    /// <summary>One position (or an instrument held before).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="key">The instrument key.</param>
    /// <param name="range">The chart range.</param>
    /// <returns>The detail, or not found.</returns>
    Task<ServiceApiResponse<PositionDetailDTO>> PositionAsync(string userId, string key, ChartRange range);

    /// <summary>A page of the activity.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="kind">Only one kind (null: everything).</param>
    /// <param name="skip">Lines to skip.</param>
    /// <param name="take">Lines to return.</param>
    /// <returns>The page.</returns>
    Task<ServiceApiResponse<ActivityPageDTO>> ActivityAsync(string userId, ActivityKind? kind, int skip, int take);

    /// <summary>The Income screen of a year.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="year">The year (this one when null).</param>
    /// <returns>The income.</returns>
    Task<ServiceApiResponse<IncomeDTO>> IncomeAsync(string userId, int? year);
}

/// <summary>
/// Builds the screens from <see cref="PortfolioMath"/> and <see cref="IncomeMath"/>.
/// </summary>
/// <param name="loader">Loads an account.</param>
/// <param name="connectionRepository">The Trading 212 connection.</param>
/// <param name="globalizationHelper">Messages.</param>
internal sealed class PortfolioService(
    PortfolioLoader loader,
    IConnectionRepository connectionRepository,
    IGlobalizationHelper globalizationHelper) : IPortfolioService
{
    /// <inheritdoc />
    public async Task<ServiceApiResponse<OverviewDTO>> OverviewAsync(string userId, ChartRange range)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<OverviewDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        var connection = await connectionRepository.GetAsync(id);
        var positions = PortfolioMath.Positions(data);
        var cash = PortfolioMath.CashNow(data);
        var positionsValue = positions.Sum(static position => position.Value);
        var total = positionsValue + cash;
        var cost = positions.Sum(static position => position.Cost);
        var deposits = PortfolioMath.NetDeposits(data);

        var firstDay = PortfolioMath.FirstDay(data);
        var rangeStart = PortfolioMath.RangeStart(range, data.Today, firstDay);
        var yearStart = PortfolioMath.RangeStart(ChartRange.Year, data.Today, firstDay);
        var series = PortfolioMath.ValueSeries(data, positions, cash, rangeStart < yearStart ? rangeStart : yearStart);
        var rangeSeries = series.Where(point => point.Day >= rangeStart).ToList();
        var yearSeries = series.Where(point => point.Day >= yearStart).ToList();
        var rangeChange = PortfolioMath.RangeChange(rangeSeries);

        var dayChange = positions.Sum(static position => position.DayChange);
        var received = IncomeMath.Received(data);
        var expected = IncomeMath.Expected(data, positions);
        var thisYear = received.Where(payment => payment.Day.Year == data.Today.Year).Sum(static payment => payment.Amount);
        var stillThisYear = expected.Where(payment => payment.Day.Year == data.Today.Year).Sum(static payment => payment.Amount);

        var allocation = positions.GroupBy(static position => position.Instrument.Class)
            .Select(group => new AllocationSliceDTO { Key = group.Key.ToString(), Label = PortfolioMath.Label(group.Key), Value = group.Sum(static position => position.Value) })
            .OrderByDescending(static slice => slice.Value)
            .ToList();
        if (cash > 0)
        {
            allocation.Add(new AllocationSliceDTO { Key = "Cash", Label = "Cash", Value = cash });
        }

        foreach (var slice in allocation)
        {
            slice.Percent = total > 0 ? slice.Value / total : 0m;
        }

        var positionDtos = positions.Select(position => ToDTO(position, positionsValue, data)).ToList();
        var (pies, outsidePies) = PieMath.Group(data, positions);
        return ServiceApiResponse<OverviewDTO>.Success(new OverviewDTO
        {
            Currency = data.Currency,
            HasData = data.HasData,
            Connected = connection is not null,
            LastSyncAt = Utc(connection?.LastSyncAt),
            TotalValue = total,
            Cash = cash,
            Cost = cost,
            Unrealized = positionsValue - cost,
            UnrealizedPct = cost > 0 ? (positionsValue - cost) / cost : null,
            NetDeposits = deposits,
            TotalReturn = deposits is { } put ? total - put : null,
            TotalReturnPct = deposits is > 0 ? (total - deposits.Value) / deposits.Value : null,
            DayChange = dayChange,
            DayChangePct = total - dayChange > 0 && dayChange != 0 ? dayChange / (total - dayChange) : null,
            Range = range,
            RangeChange = rangeChange?.Change,
            RangeChangePct = rangeChange?.Pct,
            YearChangePct = PortfolioMath.RangeChange(yearSeries)?.Pct,
            VsWorld = Insights.Services.InsightsService.Performance(data, range).Benchmarks
                .FirstOrDefault(static benchmark => benchmark.Key == Instruments.Services.Benchmarks.World.Key && benchmark.Available)?.Difference,
            Series = PortfolioMath.Thin(rangeSeries).Select(static point => new ValuePointDTO { Day = point.Day, Value = point.Value, Deposits = point.Deposits }).ToList(),
            Allocation = allocation,
            Movers = positionDtos.Where(static position => position.DayChangePct is not null && position.DayChangePct != 0)
                .OrderByDescending(static position => Math.Abs(position.DayChangePct!.Value)).Take(4).ToList(),
            Positions = positionDtos,
            Pies = pies.Select(pie => ToDTO(pie, positionsValue)).ToList(),
            OutsidePies = pies.Count == 0 ? [] : outsidePies.Select(slice => ToDTO(slice, positionsValue)).ToList(),
            IncomeThisYear = thisYear,
            IncomeNext12 = expected.Sum(static payment => payment.Amount),
            IncomeProgress = thisYear + stillThisYear > 0 ? thisYear / (thisYear + stillThisYear) : null,
            Upcoming = expected.Take(3).Select(payment => ToDTO(payment, data)).ToList(),
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<HoldingsDTO>> HoldingsAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<HoldingsDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        var positions = PortfolioMath.Positions(data);
        var value = positions.Sum(static position => position.Value);
        var cost = positions.Sum(static position => position.Cost);
        return ServiceApiResponse<HoldingsDTO>.Success(new HoldingsDTO
        {
            Currency = data.Currency,
            Value = value,
            Cost = cost,
            Unrealized = value - cost,
            UnrealizedPct = cost > 0 ? (value - cost) / cost : null,
            Cash = PortfolioMath.CashNow(data),
            Positions = positions.Select(position => ToDTO(position, value, data)).ToList(),
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PositionDetailDTO>> PositionAsync(string userId, string key, ChartRange range)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<PositionDetailDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        var instrument = data.Instruments.Values.FirstOrDefault(row => string.Equals(row.InstrumentKey, key, StringComparison.OrdinalIgnoreCase));
        var trades = instrument is null ? [] : data.Trades.Where(trade => trade.InstrumentID == instrument.InstrumentID).ToList();
        var positions = PortfolioMath.Positions(data);
        var position = instrument is null ? null : positions.FirstOrDefault(position => position.Instrument.InstrumentID == instrument.InstrumentID);
        if (instrument is null || (position is null && trades.Count == 0 && data.Dividends.All(dividend => dividend.InstrumentID != instrument.InstrumentID)))
        {
            return await NotFoundAsync<PositionDetailDTO>(key);
        }

        var series = data.Prices.Series(instrument.InstrumentID);
        var firstDay = trades.Count > 0 ? data.LocalDay(trades[0].ExecutedAt)
            : position?.OpenedAt is { } opened ? data.LocalDay(opened)
            : series.Count > 0 ? series[0].Key : data.Today;
        if (series.Count > 0 && series[0].Key > firstDay)
        {
            firstDay = series[0].Key;
        }

        var start = PortfolioMath.RangeStart(range, data.Today, range == ChartRange.All ? firstDay : (series.Count > 0 ? series[0].Key : firstDay));
        var prices = new List<PricePointDTO>();
        for (var day = start; day <= data.Today; day = day.AddDays(1))
        {
            if (data.Prices.PriceOn(instrument.InstrumentID, day) is { } close)
            {
                prices.Add(new PricePointDTO { Day = day, Close = close });
            }
        }

        var allValue = positions.Sum(static row => row.Value);
        var dividends = data.Dividends.Where(dividend => dividend.InstrumentID == instrument.InstrumentID).ToList();
        var activity = trades.Select(trade => TradeItem(trade, instrument, data))
            .Concat(dividends.Select(dividend => new ActivityItemDTO
            {
                Kind = ActivityKind.Dividend,
                At = Utc(dividend.PaidAt)!.Value,
                Instrument = ToDTO(instrument),
                Quantity = dividend.Quantity,
                Price = dividend.PerShare,
                PriceCurrency = instrument.Currency,
                Amount = dividend.Amount,
                Note = Pretty(dividend.Kind),
                Source = (DataSource)dividend.Source,
            }))
            .OrderByDescending(static item => item.At)
            .ToList();

        return ServiceApiResponse<PositionDetailDTO>.Success(new PositionDetailDTO
        {
            Currency = data.Currency,
            Instrument = ToDTO(instrument),
            Position = position is null ? null : ToDTO(position, allValue, data),
            Range = range,
            Prices = PortfolioMath.Thin(prices),
            RangeChangePct = prices.Count > 1 && prices[0].Close > 0 ? prices[^1].Close / prices[0].Close - 1m : null,
            Marks = trades.Where(trade => data.LocalDay(trade.ExecutedAt) >= start)
                .Select(trade => new TradeMarkDTO { Day = data.LocalDay(trade.ExecutedAt), Buy = trade.IsBuy, Quantity = trade.Quantity, Price = trade.Price })
                .ToList(),
            Dividends = dividends.Sum(static dividend => dividend.Amount),
            Realized = trades.Where(static trade => !trade.IsBuy).Sum(static trade => trade.RealizedPL ?? 0m),
            Invested = trades.Where(static trade => trade.IsBuy).Sum(static trade => trade.Total),
            Activity = activity,
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ActivityPageDTO>> ActivityAsync(string userId, ActivityKind? kind, int skip, int take)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<ActivityPageDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        var items = AllActivity(data).Where(item => kind is null || item.Kind == kind).ToList();
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);
        return ServiceApiResponse<ActivityPageDTO>.Success(new ActivityPageDTO
        {
            Currency = data.Currency,
            Total = items.Count,
            Skip = skip,
            Take = take,
            Items = items.Skip(skip).Take(take).ToList(),
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<IncomeDTO>> IncomeAsync(string userId, int? year)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<IncomeDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        var positions = PortfolioMath.Positions(data);
        var received = IncomeMath.Received(data);
        var expected = IncomeMath.Expected(data, positions);
        var byPosition = IncomeMath.ByPosition(data, positions);
        var years = received.Select(static payment => payment.Day.Year).Append(data.Today.Year).Distinct().OrderByDescending(static value => value).ToList();
        var shown = year is { } asked && years.Contains(asked) ? asked : data.Today.Year;
        var isCurrent = shown == data.Today.Year;

        var inYear = received.Where(payment => payment.Day.Year == shown).ToList();
        var total = inYear.Sum(static payment => payment.Amount);
        var interest = inYear.Where(static payment => payment.InstrumentId is null).Sum(static payment => payment.Amount);
        var still = isCurrent ? expected.Where(payment => payment.Day.Year == shown).ToList() : [];
        var cost = positions.Sum(static position => position.Cost);
        var next12 = expected.Sum(static payment => payment.Amount);

        var months = Enumerable.Range(1, 12).Select(month => new IncomeMonthDTO
        {
            Month = month,
            Received = inYear.Where(payment => payment.Day.Month == month).Sum(static payment => payment.Amount),
            Expected = still.Where(payment => payment.Day.Month == month).Sum(static payment => payment.Amount),
            IsFuture = isCurrent && month >= data.Today.Month,
        }).ToList();

        var bySource = inYear.Where(static payment => payment.InstrumentId is not null)
            .GroupBy(static payment => payment.InstrumentId!.Value)
            .Select(group => (Id: group.Key, Amount: group.Sum(static payment => payment.Amount)))
            .OrderByDescending(static pair => pair.Amount)
            .ToList();
        var sources = bySource.Take(3).Where(pair => data.Instruments.ContainsKey(pair.Id)).Select(pair => new IncomeSourceDTO
        {
            Instrument = ToDTO(data.Instruments[pair.Id]),
            Label = data.Instruments[pair.Id].DisplaySymbol,
            Amount = pair.Amount,
        }).ToList();
        var other = bySource.Skip(3).Sum(static pair => pair.Amount);
        if (other > 0)
        {
            sources.Add(new IncomeSourceDTO { Label = "Other", Amount = other });
        }

        if (interest > 0)
        {
            sources.Add(new IncomeSourceDTO { Label = "Interest", Amount = interest });
        }

        foreach (var source in sources)
        {
            source.Percent = total > 0 ? source.Amount / total : 0m;
        }

        return ServiceApiResponse<IncomeDTO>.Success(new IncomeDTO
        {
            Currency = data.Currency,
            Year = shown,
            Years = years,
            IsCurrentYear = isCurrent,
            Received = total,
            Interest = interest,
            ExpectedYear = total + still.Sum(static payment => payment.Amount),
            Progress = isCurrent && total + still.Sum(static payment => payment.Amount) > 0 ? total / (total + still.Sum(static payment => payment.Amount)) : null,
            Next12 = next12,
            YieldOnCost = cost > 0 && next12 > 0 ? next12 / cost : null,
            Months = months,
            Upcoming = expected.Take(6).Select(payment => ToDTO(payment, data)).ToList(),
            Sources = sources,
            Payments = AllActivity(data).Where(item => item.Kind is ActivityKind.Dividend or ActivityKind.Interest && data.LocalDay(item.At.UtcDateTime).Year == shown).ToList(),
            Positions = byPosition.Where(item => data.Instruments.ContainsKey(item.InstrumentId)).Select(item => new IncomePositionDTO
            {
                Instrument = ToDTO(data.Instruments[item.InstrumentId]),
                Value = item.Value,
                Received = item.Received,
                Yield = item.Value > 0 && item.Received > 0 ? item.Received / item.Value : null,
                YieldOnCost = item.Cost > 0 && item.Received > 0 ? item.Received / item.Cost : null,
                Payments = item.Payments,
                LastPerShare = item.LastPerShare,
                Change = item.Change,
                Trend = item.Trend,
                Withheld = item.Withheld,
            }).ToList(),
            Withheld = byPosition.Sum(static item => item.Withheld),
        });
    }

    /// <summary>
    /// The DTO of an instrument.
    /// </summary>
    /// <param name="instrument">The instrument.</param>
    /// <returns>The DTO.</returns>
    internal static InstrumentDTO ToDTO(InstrumentRow instrument) => new()
    {
        Key = instrument.InstrumentKey,
        Isin = instrument.Isin,
        Symbol = instrument.DisplaySymbol,
        Name = instrument.Name,
        Currency = instrument.Currency,
        Class = instrument.Class,
        Ticker = instrument.Ticker,
    };

    private static PositionDTO ToDTO(Position position, decimal allValue, PortfolioData data)
    {
        var series = data.Prices.Series(position.Instrument.InstrumentID);
        var since = data.Today.AddDays(-45);
        return new PositionDTO
        {
            Instrument = ToDTO(position.Instrument),
            Quantity = position.Quantity,
            AveragePrice = position.AveragePrice,
            Price = position.Price,
            Value = position.Value,
            Cost = position.Cost,
            Unrealized = position.Unrealized,
            UnrealizedPct = position.UnrealizedPct,
            Weight = allValue > 0 ? position.Value / allValue : 0m,
            DayChange = position.DayChange,
            DayChangePct = position.DayChangePct,
            Spark = series.Where(point => point.Key >= since).Select(static point => point.Value).TakeLast(30).ToList(),
            OpenedAt = Utc(position.OpenedAt),
        };
    }

    private static PieDTO ToDTO(PieGroup pie, decimal allValue) => new()
    {
        Id = pie.Pie.PieID,
        Name = pie.Pie.Name,
        Icon = pie.Pie.Icon,
        Value = pie.Value,
        Invested = pie.Invested,
        Gain = pie.Gain,
        GainPct = pie.GainPct,
        Weight = allValue > 0 ? pie.Value / allValue : 0m,
        Cash = pie.Pie.Cash,
        DividendsGained = pie.Pie.DividendsGained,
        Goal = pie.Pie.Goal,
        Holdings = pie.Holdings.Select(slice => ToDTO(slice, allValue)).ToList(),
    };

    private static PieHoldingDTO ToDTO(PieSlice slice, decimal allValue) => new()
    {
        Instrument = ToDTO(slice.Instrument),
        Quantity = slice.Quantity,
        Value = slice.Value,
        Invested = slice.Invested,
        Gain = slice.Gain,
        GainPct = slice.GainPct,
        Weight = allValue > 0 ? slice.Value / allValue : 0m,
        CurrentShare = slice.CurrentShare,
        ExpectedShare = slice.ExpectedShare,
    };

    private static UpcomingPaymentDTO ToDTO(ExpectedPayment payment, PortfolioData data) => new()
    {
        Instrument = data.Instruments.TryGetValue(payment.InstrumentId, out var instrument) ? ToDTO(instrument) : new InstrumentDTO(),
        Day = payment.Day,
        Amount = payment.Amount,
    };

    private static ActivityItemDTO TradeItem(TradeRow trade, InstrumentRow instrument, PortfolioData data) => new()
    {
        Kind = trade.IsBuy ? ActivityKind.Buy : ActivityKind.Sell,
        At = Utc(trade.ExecutedAt)!.Value,
        Instrument = ToDTO(instrument),
        Quantity = trade.Quantity,
        Price = trade.Price,
        PriceCurrency = trade.PriceCurrency,
        Amount = trade.IsBuy ? -trade.Total : trade.Total,
        Note = trade.RealizedPL is { } realized && !trade.IsBuy ? $"Realised {realized.ToString("+0.00;-0.00", System.Globalization.CultureInfo.InvariantCulture)} {data.Currency}" : null,
        Source = (DataSource)trade.Source,
    };

    private static List<ActivityItemDTO> AllActivity(PortfolioData data)
    {
        var items = new List<ActivityItemDTO>();
        foreach (var trade in data.Trades)
        {
            if (data.Instruments.TryGetValue(trade.InstrumentID, out var instrument))
            {
                items.Add(TradeItem(trade, instrument, data));
            }
        }

        foreach (var dividend in data.Dividends)
        {
            data.Instruments.TryGetValue(dividend.InstrumentID, out var instrument);
            items.Add(new ActivityItemDTO
            {
                Kind = ActivityKind.Dividend,
                At = Utc(dividend.PaidAt)!.Value,
                Instrument = instrument is null ? null : ToDTO(instrument),
                Quantity = dividend.Quantity,
                Price = dividend.PerShare,
                PriceCurrency = instrument?.Currency,
                Amount = dividend.Amount,
                Note = Pretty(dividend.Kind),
                Source = (DataSource)dividend.Source,
            });
        }

        foreach (var movement in data.Cash)
        {
            items.Add(new ActivityItemDTO
            {
                Kind = (CashKind)movement.Kind switch
                {
                    CashKind.Deposit => ActivityKind.Deposit,
                    CashKind.Withdrawal => ActivityKind.Withdrawal,
                    CashKind.Interest => ActivityKind.Interest,
                    CashKind.Fee => ActivityKind.Fee,
                    _ => movement.Amount >= 0 ? ActivityKind.Deposit : ActivityKind.Withdrawal,
                },
                At = Utc(movement.At)!.Value,
                Amount = movement.Amount,
                Note = movement.Note.Length > 0 ? movement.Note : null,
                Source = (DataSource)movement.Source,
            });
        }

        return items.OrderByDescending(static item => item.At).ToList();
    }

    private static string? Pretty(string kind)
    {
        var text = (kind ?? string.Empty).Replace('_', ' ').Trim().ToLowerInvariant();
        return text.Length == 0 ? null : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static DateTimeOffset? Utc(DateTime? value) =>
        value is { } time ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)) : null;

    private async Task<ServiceApiResponse<T>> NotFoundAsync<T>(string what) =>
        ServiceApiResponse<T>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), what ?? string.Empty));
}
