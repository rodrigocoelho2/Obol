using Obol.Library.Enums;
using Obol.Library.Services;
using Obol.WebApi.Extensions.Globalization;
using Obol.WebApi.Features.Insights.Models.DTOs;
using Obol.WebApi.Features.Instruments.Services;
using Obol.WebApi.Features.Portfolio.Services;

namespace Obol.WebApi.Features.Insights.Services;

/// <summary>
/// The Insights screens: performance against the indexes, exposure, realized gains and the goals' starting point.
/// </summary>
internal interface IInsightsService
{
    /// <summary>How the money did over a range, against MSCI World and the S&amp;P 500.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="range">The range.</param>
    /// <returns>The performance.</returns>
    Task<ServiceApiResponse<PerformanceDTO>> PerformanceAsync(string userId, ChartRange range);

    /// <summary>Currencies, sectors, countries and concentration.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The exposure.</returns>
    Task<ServiceApiResponse<ExposureDTO>> ExposureAsync(string userId);

    /// <summary>What the sales of a year made.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="year">The year (this one when null).</param>
    /// <returns>The realized gains.</returns>
    Task<ServiceApiResponse<RealizedDTO>> RealizedAsync(string userId, int? year);

    /// <summary>Where the portfolio is now, for the projection.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The starting point.</returns>
    Task<ServiceApiResponse<GoalsDTO>> GoalsAsync(string userId);
}

/// <summary>
/// Builds the Insights screens from <see cref="InsightsMath"/> on the data <see cref="PortfolioLoader"/> reads.
/// </summary>
/// <param name="loader">Loads an account.</param>
/// <param name="globalizationHelper">Messages.</param>
internal sealed class InsightsService(PortfolioLoader loader, IGlobalizationHelper globalizationHelper) : IInsightsService
{
    private const int MaxPoints = 240;
    private const int HeatmapMonths = 36;

    /// <summary>The growth scenarios of the projection.</summary>
    internal static readonly IReadOnlyList<GoalScenarioDTO> Scenarios =
    [
        new GoalScenarioDTO { Key = "low", Label = "Careful", Rate = 0.04m },
        new GoalScenarioDTO { Key = "mid", Label = "Steady", Rate = 0.07m },
        new GoalScenarioDTO { Key = "high", Label = "Strong", Rate = 0.10m },
    ];

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PerformanceDTO>> PerformanceAsync(string userId, ChartRange range)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<PerformanceDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        return ServiceApiResponse<PerformanceDTO>.Success(Performance(data, range));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ExposureDTO>> ExposureAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<ExposureDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        return ServiceApiResponse<ExposureDTO>.Success(Exposure(data));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<RealizedDTO>> RealizedAsync(string userId, int? year)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<RealizedDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        return ServiceApiResponse<RealizedDTO>.Success(Realized(data, year));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<GoalsDTO>> GoalsAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<GoalsDTO>(userId);
        }

        var data = await loader.LoadAsync(id);
        return ServiceApiResponse<GoalsDTO>.Success(Goals(data));
    }

    /// <summary>The Performance screen of an account.</summary>
    /// <param name="data">The account.</param>
    /// <param name="range">The range.</param>
    /// <returns>The performance.</returns>
    internal static PerformanceDTO Performance(PortfolioData data, ChartRange range)
    {
        var result = new PerformanceDTO { Currency = data.Currency, Range = range };
        if (!data.HasData)
        {
            return result;
        }

        var positions = PortfolioMath.Positions(data);
        var cash = PortfolioMath.CashNow(data);
        var firstDay = PortfolioMath.FirstDay(data);
        var (flows, fromTransfers) = InsightsMath.Flows(data);
        result.FromTransfers = fromTransfers;

        // The whole history once (the monthly returns need it), then the range.
        var all = PortfolioMath.ValueSeries(data, positions, cash, firstDay)
            .Select(point => (point.Day, Value: InsightsMath.Measured(point, fromTransfers)))
            .SkipWhile(static point => point.Value <= 0)
            .ToList();
        if (all.Count < 2)
        {
            return result;
        }

        var allIndex = InsightsMath.TimeWeighted(all, flows.Where(pair => pair.Key > all[0].Day).ToDictionary(static pair => pair.Key, static pair => pair.Value));
        result.Months = InsightsMath.Monthly(allIndex).TakeLast(HeatmapMonths)
            .Select(static month => new MonthReturnDTO { Year = month.Year, Month = month.Month, Return = month.Return }).ToList();

        var from = PortfolioMath.RangeStart(range, data.Today, all[0].Day);
        var values = all.Where(point => point.Day >= from).ToList();
        if (values.Count < 2)
        {
            return result;
        }

        var days = values.Select(static point => point.Day).ToList();
        var start = values[0].Value;
        var inRange = flows.Where(pair => pair.Key > days[0] && pair.Key <= days[^1]).ToDictionary(static pair => pair.Key, static pair => pair.Value);
        var index = InsightsMath.TimeWeighted(values, inRange);
        var span = days[^1].DayNumber - days[0].DayNumber;
        var timeWeighted = index[^1].Index - 1m;
        var invested = start + inRange.Values.Sum();
        var xirr = InsightsMath.Xirr([new CashFlow(days[0], start), .. inRange.Select(static pair => new CashFlow(pair.Key, pair.Value))], days[^1], values[^1].Value);

        result.HasData = true;
        result.From = days[0];
        result.Days = span;
        result.Value = values[^1].Value;
        result.Invested = Math.Round(invested, 2);
        result.Gain = Math.Round(values[^1].Value - invested, 2);
        result.TimeWeighted = Math.Round(timeWeighted, 6);
        result.TimeWeightedYearly = span >= 365 ? Math.Round(Yearly(timeWeighted, span), 6) : null;
        result.MoneyWeightedYearly = xirr;
        result.MoneyWeighted = xirr is { } rate && span > 0 ? Math.Round((decimal)(Math.Pow(1 + (double)rate, span / 365.0) - 1), 6) : null;

        // The same money in each benchmark.
        var benchmarkValues = new List<List<decimal>?>();
        foreach (var benchmark in Benchmarks.All)
        {
            var dto = new BenchmarkResultDTO { Key = benchmark.Key, Label = benchmark.Label, Name = benchmark.Name };
            List<decimal>? line = null;
            if (data.Benchmarks.TryGetValue(benchmark.Key, out var benchmarkId) && data.Prices.Series(benchmarkId) is { Count: > 0 } closes
                && closes[0].Key <= days[0].AddDays(7) && closes[^1].Key >= days[^1].AddDays(-7))
            {
                var currency = data.Instruments.TryGetValue(benchmarkId, out var row) ? row.Currency : benchmark.Currency;
                decimal? Price(DateOnly day) => data.Prices.PriceOn(benchmarkId, day) is { } close ? close * data.Prices.FxOn(currency, day) : null;
                line = InsightsMath.SameMoneyIn(days, start, inRange, Price);
                if (line is not null && Price(days[0]) is { } first && Price(days[^1]) is { } last && first > 0)
                {
                    dto.Available = true;
                    dto.Value = line[^1];
                    dto.Return = Math.Round(last / first - 1m, 6);
                    dto.Difference = Math.Round(values[^1].Value - line[^1], 2);
                }
            }

            benchmarkValues.Add(line);
            result.Benchmarks.Add(dto);
        }

        var running = start;
        var points = new List<PerformancePointDTO>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                running += inRange.GetValueOrDefault(days[i]);
            }

            points.Add(new PerformancePointDTO
            {
                Day = days[i],
                Value = values[i].Value,
                Invested = Math.Round(running, 2),
                Benchmarks = benchmarkValues.Select(line => line is null ? (decimal?)null : line[i]).ToList(),
            });
        }

        result.Series = PortfolioMath.Thin(points, MaxPoints);
        return result;
    }

    /// <summary>The Exposure screen of an account.</summary>
    /// <param name="data">The account.</param>
    /// <returns>The exposure.</returns>
    internal static ExposureDTO Exposure(PortfolioData data)
    {
        var positions = PortfolioMath.Positions(data);
        var cash = PortfolioMath.CashNow(data);
        var invested = positions.Sum(static position => position.Value);
        var total = invested + cash;
        var result = new ExposureDTO { Currency = data.Currency, Total = total, Invested = invested };

        // Currencies: what each position is priced in (pence count as pounds), the cash in the account's own.
        var byCurrency = positions.GroupBy(static position => position.Instrument.Currency.ToUpperInvariant() == "GBX" ? "GBP" : position.Instrument.Currency.ToUpperInvariant())
            .Select(group => new ExposureSliceDTO
            {
                Key = group.Key,
                Label = group.Key,
                Value = group.Sum(static position => position.Value),
                Count = group.Count(),
                Symbols = group.OrderByDescending(static position => position.Value).Select(static position => position.Instrument.DisplaySymbol).Take(6).ToList(),
                FxImpact = group.Key == data.Currency ? null : Math.Round(group.Sum(position => FxImpact(position, data)), 2),
            })
            .ToList();
        if (cash > 0)
        {
            if (byCurrency.FirstOrDefault(slice => slice.Key == data.Currency) is { } home)
            {
                home.Value += cash;
            }
            else
            {
                byCurrency.Add(new ExposureSliceDTO { Key = data.Currency, Label = data.Currency, Value = cash });
            }
        }

        foreach (var slice in byCurrency)
        {
            slice.Percent = total > 0 ? slice.Value / total : 0m;
        }

        result.Currencies = byCurrency.OrderByDescending(static slice => slice.Value).ToList();
        result.FxImpact = Math.Round(byCurrency.Sum(static slice => slice.FxImpact ?? 0m), 2);
        result.Foreign = byCurrency.Where(slice => slice.Key != data.Currency).Sum(static slice => slice.Value);

        result.Sectors = Slices(positions, invested, static position => position.Instrument.Class switch
        {
            AssetClass.Stock => position.Instrument.Sector is { Length: > 0 } sector ? (sector, sector) : ("OTHER", "Other shares"),
            AssetClass.EquityEtf => ("EQUITY_ETF", "Equity ETFs"),
            AssetClass.BondEtf => ("BOND_ETF", "Bond ETFs"),
            AssetClass.Commodity => ("COMMODITY", "Commodities"),
            AssetClass.Crypto => ("CRYPTO", "Crypto"),
            _ => ("OTHER", "Other"),
        });
        result.Countries = Slices(positions, invested, static position => position.Instrument.Class == AssetClass.Stock
            ? InsightsMath.Country(position.Instrument.Isin)
            : ("FUND", "Funds (many countries)"));

        var weights = positions.Where(static position => position.Value > 0).Select(position => invested > 0 ? position.Value / invested : 0m).ToList();
        result.Concentration = new ConcentrationDTO
        {
            Positions = positions.Count,
            TopSymbol = positions.FirstOrDefault()?.Instrument.Symbol ?? string.Empty,
            TopWeight = weights.Count > 0 ? weights.Max() : 0m,
            Top5Weight = weights.OrderByDescending(static weight => weight).Take(5).Sum(),
            Effective = weights.Count > 0 && weights.Sum(static weight => weight * weight) is var squares && squares > 0 ? Math.Round(1m / squares, 1) : 0m,
        };
        return result;
    }

    /// <summary>The Realized screen of an account.</summary>
    /// <param name="data">The account.</param>
    /// <param name="year">The year asked for.</param>
    /// <returns>The realized gains.</returns>
    internal static RealizedDTO Realized(PortfolioData data, int? year)
    {
        var sales = InsightsMath.Sales(data.Trades).Where(sale => data.Instruments.ContainsKey(sale.Trade.InstrumentID)).ToList();
        int YearOf(Sale sale) => data.LocalDay(sale.Trade.ExecutedAt).Year;
        var years = sales.Select(YearOf).Append(data.Today.Year).Distinct().OrderByDescending(static value => value).ToList();
        var shown = year is { } asked && years.Contains(asked) ? asked : data.Today.Year;
        var inYear = sales.Where(sale => YearOf(sale) == shown).ToList();

        return new RealizedDTO
        {
            Currency = data.Currency,
            Year = shown,
            Years = years,
            Gain = inYear.Sum(static sale => sale.Gain ?? 0m),
            Proceeds = inYear.Sum(static sale => sale.Trade.Total),
            Cost = inYear.Sum(static sale => sale.Cost ?? 0m),
            Wins = inYear.Count(static sale => sale.Gain > 0),
            Losses = inYear.Count(static sale => sale.Gain < 0),
            Unknown = inYear.Count(static sale => sale.Gain is null),
            Sales = inYear.OrderByDescending(static sale => sale.Trade.ExecutedAt).Select(sale => new RealizedSaleDTO
            {
                Instrument = PortfolioService.ToDTO(data.Instruments[sale.Trade.InstrumentID]),
                At = DateTime.SpecifyKind(sale.Trade.ExecutedAt, DateTimeKind.Utc),
                Quantity = sale.Trade.Quantity,
                Price = sale.Trade.Price,
                PriceCurrency = sale.Trade.PriceCurrency,
                Proceeds = sale.Trade.Total,
                Cost = sale.Cost,
                Gain = sale.Gain,
                GainPct = sale.Gain is { } gain && sale.Cost is > 0 ? gain / sale.Cost.Value : null,
            }).ToList(),
            ByYear = sales.GroupBy(YearOf).OrderBy(static group => group.Key)
                .Select(static group => new RealizedGroupDTO { Year = group.Key, Gain = group.Sum(static sale => sale.Gain ?? 0m), Sales = group.Count() }).ToList(),
            ByInstrument = inYear.GroupBy(static sale => sale.Trade.InstrumentID)
                .Select(group => new RealizedGroupDTO
                {
                    Instrument = PortfolioService.ToDTO(data.Instruments[group.Key]),
                    Gain = group.Sum(static sale => sale.Gain ?? 0m),
                    Sales = group.Count(),
                })
                .OrderByDescending(static group => Math.Abs(group.Gain)).ToList(),
        };
    }

    /// <summary>The starting point of the projection.</summary>
    /// <param name="data">The account.</param>
    /// <returns>The goals' data.</returns>
    internal static GoalsDTO Goals(PortfolioData data)
    {
        var positions = PortfolioMath.Positions(data);
        var cash = PortfolioMath.CashNow(data);
        var invested = positions.Sum(static position => position.Value);
        var (flows, _) = InsightsMath.Flows(data);
        var lastYear = flows.Where(pair => pair.Key > data.Today.AddYears(-1)).Sum(static pair => pair.Value);
        var income = IncomeMath.Expected(data, positions).Sum(static payment => payment.Amount);
        var performance = Performance(data, ChartRange.All);

        return new GoalsDTO
        {
            Currency = data.Currency,
            Value = invested + cash,
            Invested = PortfolioMath.NetDeposits(data),
            MonthlyAverage = Math.Max(0m, Math.Round(lastYear / 12m, 0)),
            IncomeNext12 = income,
            Yield = invested > 0 && income > 0 ? Math.Round(income / invested, 6) : null,
            YearlyReturn = performance.Days >= 180 ? performance.MoneyWeightedYearly : null,
            Scenarios = Scenarios.Select(static scenario => new GoalScenarioDTO { Key = scenario.Key, Label = scenario.Label, Rate = scenario.Rate }).ToList(),
        };
    }

    /// <summary>
    /// What the exchange rate added to (or took from) a position since it was bought: Trading 212's figure, or the
    /// shares at today's price times the change of the rate since the average buy (worked out from cost and price).
    /// </summary>
    /// <param name="position">The position.</param>
    /// <param name="data">The account.</param>
    /// <returns>The effect, in the account currency.</returns>
    internal static decimal FxImpact(Position position, PortfolioData data)
    {
        if (position.FxImpact is { } given)
        {
            return given;
        }

        var currency = position.Instrument.Currency;
        if (string.Equals(currency, data.Currency, StringComparison.OrdinalIgnoreCase) || position.Quantity <= 0 || position.AveragePrice <= 0 || position.Cost <= 0)
        {
            return 0m;
        }

        var boughtAt = position.Cost / (position.Quantity * position.AveragePrice);
        var now = data.Prices.FxOn(currency, data.Today);
        return position.Quantity * position.Price * (now - boughtAt);
    }

    private static decimal Yearly(decimal total, int days) => (decimal)(Math.Pow(1 + (double)total, 365.0 / days) - 1);

    private static List<ExposureSliceDTO> Slices(IReadOnlyList<Position> positions, decimal total, Func<Position, (string Key, string Label)> key) =>
        positions.GroupBy(key)
            .Select(group => new ExposureSliceDTO
            {
                Key = group.Key.Key,
                Label = group.Key.Label,
                Value = group.Sum(static position => position.Value),
                Percent = total > 0 ? group.Sum(static position => position.Value) / total : 0m,
                Count = group.Count(),
                Symbols = group.OrderByDescending(static position => position.Value).Select(static position => position.Instrument.DisplaySymbol).Take(6).ToList(),
            })
            .OrderByDescending(static slice => slice.Value)
            .ToList();

    private async Task<ServiceApiResponse<T>> NotFoundAsync<T>(string? what) =>
        ServiceApiResponse<T>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), what ?? string.Empty));
}
