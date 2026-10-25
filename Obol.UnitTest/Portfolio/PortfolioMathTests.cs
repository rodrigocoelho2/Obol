using Obol.Library.Enums;
using Obol.WebApi.Features.Portfolio.Models.Entities;
using Obol.WebApi.Features.Portfolio.Services;
using static Obol.UnitTest.TestSupport;

namespace Obol.UnitTest.Portfolio;

/// <summary>
/// The portfolio maths on small hand-made accounts: average cost, positions, cash, the value of every day and the gain
/// over a range.
/// </summary>
public sealed class PortfolioMathTests
{
    [Fact]
    public void LotsUseTheAverageCostAndASellTakesItsShareOfTheCost()
    {
        var trades = new[]
        {
            Buy(1, Today.AddDays(-30), 10, 10m),
            Buy(1, Today.AddDays(-20), 10, 20m),
            Sell(1, Today.AddDays(-10), 5, 25m),
        };

        var lot = PortfolioMath.Lots(trades)[1];

        Assert.Equal(15m, lot.Quantity);
        Assert.Equal(15m, lot.AveragePrice);
        Assert.Equal(225m, lot.Cost);
        Assert.Equal(At(Today.AddDays(-30)), lot.OpenedAt);
    }

    [Fact]
    public void SellingEverythingClosesTheLotAndABuyAfterOpensItAgain()
    {
        var trades = new[]
        {
            Buy(1, Today.AddDays(-30), 4, 10m),
            Sell(1, Today.AddDays(-20), 4, 12m),
            Buy(1, Today.AddDays(-5), 2, 15m),
        };

        var lot = PortfolioMath.Lots(trades)[1];

        Assert.Equal(2m, lot.Quantity);
        Assert.Equal(15m, lot.AveragePrice);
        Assert.Equal(30m, lot.Cost);
        Assert.Equal(At(Today.AddDays(-5)), lot.OpenedAt);
    }

    [Fact]
    public void PositionsFromTradesAreValuedAtTodaysPriceWithTheDayChange()
    {
        var prices = new PriceBook("EUR");
        prices.AddPrice(1, Today.AddDays(-1), 11m);
        prices.AddPrice(1, Today, 12m);
        var data = Data([Instrument(1, "IWDA")], trades: [Buy(1, Today.AddDays(-30), 10, 10m)], prices: prices);

        var position = Assert.Single(PortfolioMath.Positions(data));

        Assert.Equal(120m, position.Value);
        Assert.Equal(100m, position.Cost);
        Assert.Equal(20m, position.Unrealized);
        Assert.Equal(0.2m, position.UnrealizedPct);
        Assert.Equal(10m, position.DayChange);
        Assert.Equal(12m / 11m - 1m, position.DayChangePct);
    }

    [Fact]
    public void ForeignPositionsAreConvertedAtTheRateOfTheDay()
    {
        var prices = new PriceBook("EUR");
        prices.AddPrice(2, Today, 110m);
        prices.AddFx("USD", Today.AddDays(-3), 0.9m);
        var data = Data([Instrument(2, "AAPL", "USD", AssetClass.Stock)], trades: [Buy(2, Today.AddDays(-40), 2, 100m, total: 180m, currency: "USD")], prices: prices);

        var position = Assert.Single(PortfolioMath.Positions(data));

        Assert.Equal(198m, position.Value);
        Assert.Equal(180m, position.Cost);
    }

    [Fact]
    public void WithoutAPriceAPositionIsValuedAtWhatItCost()
    {
        var data = Data([Instrument(1, "IWDA")], trades: [Buy(1, Today.AddDays(-3), 3, 50m)]);

        var position = Assert.Single(PortfolioMath.Positions(data));

        Assert.Equal(50m, position.Price);
        Assert.Equal(150m, position.Value);
        Assert.Null(position.DayChangePct);
    }

    [Fact]
    public void PositionsComeFromTradingTwelveWhenTheAccountIsSynced()
    {
        var data = new PortfolioData
        {
            Currency = "EUR",
            Today = Today,
            Instruments = new Dictionary<int, Obol.WebApi.Features.Instruments.Models.Entities.InstrumentRow> { [1] = Instrument(1, "IWDA") },
            Holdings =
            [
                new HoldingRow { InstrumentID = 1, Quantity = 7, AveragePrice = 90m, CurrentPrice = 100m, Value = 700m, Cost = 630m, UpdatedAt = At(Today) },
                new HoldingRow { InstrumentID = 1, Quantity = 0, Value = 0m },
            ],
            BrokerHoldings = true,
            Trades = [Buy(1, Today.AddDays(-9), 3, 80m)],
            Dividends = [],
            Cash = [Cash(CashKind.Deposit, Today.AddDays(-10), 1000m)],
            Snapshots = [new SnapshotRow { Day = At(Today), Cash = 42.5m, TotalValue = 742.5m }],
            Prices = new PriceBook("EUR"),
            LocalDay = static utc => DateOnly.FromDateTime(utc),
        };

        var position = Assert.Single(PortfolioMath.Positions(data));

        Assert.Equal(7m, position.Quantity);
        Assert.Equal(700m, position.Value);
        Assert.Equal(42.5m, PortfolioMath.CashNow(data));
    }

    [Fact]
    public void CashWithoutASyncIsEverythingThatMovedIt()
    {
        var data = Data(
            [Instrument(1, "IWDA")],
            trades: [Buy(1, Today.AddDays(-20), 2, 50m), Sell(1, Today.AddDays(-2), 1, 60m)],
            cash: [Cash(CashKind.Deposit, Today.AddDays(-30), 1000m), Cash(CashKind.Interest, Today.AddDays(-1), 1.25m), Cash(CashKind.Withdrawal, Today.AddDays(-1), -50m)],
            dividends: [Dividend(1, Today.AddDays(-5), 5m)]);

        Assert.Equal(1000m - 100m + 60m + 1.25m - 50m + 5m, PortfolioMath.CashNow(data));
        Assert.Equal(950m, PortfolioMath.NetDeposits(data));
    }

    [Fact]
    public void NetDepositsAreUnknownWithoutTransfers()
    {
        var data = Data([Instrument(1, "IWDA")], trades: [Buy(1, Today.AddDays(-20), 2, 50m)]);

        Assert.Null(PortfolioMath.NetDeposits(data));
        Assert.Equal(0m, PortfolioMath.CashNow(data));
    }

    [Fact]
    public void TheValueOfEveryDayIsRebuiltBackwardsFromToday()
    {
        var prices = new PriceBook("EUR");
        prices.AddPrice(1, Today.AddDays(-2), 50m);
        prices.AddPrice(1, Today.AddDays(-1), 55m);
        prices.AddPrice(1, Today, 60m);
        var data = Data(
            [Instrument(1, "IWDA")],
            trades: [Buy(1, Today.AddDays(-2), 10, 50m)],
            cash: [Cash(CashKind.Deposit, Today.AddDays(-3), 1000m)],
            prices: prices);
        var positions = PortfolioMath.Positions(data);
        var cash = PortfolioMath.CashNow(data);

        var series = PortfolioMath.ValueSeries(data, positions, cash, Today.AddDays(-3));

        Assert.Equal(500m, cash);
        Assert.Equal(new[] { Today.AddDays(-3), Today.AddDays(-2), Today.AddDays(-1), Today }, series.Select(static point => point.Day));
        Assert.Equal(new[] { 1000m, 1000m, 1050m, 1100m }, series.Select(static point => point.Value));
        Assert.All(series, static point => Assert.Equal(1000m, point.Deposits));

        var change = PortfolioMath.RangeChange(series);
        Assert.NotNull(change);
        Assert.Equal(100m, change.Value.Change);
        Assert.Equal(0.1m, change.Value.Pct);
    }

    [Fact]
    public void TheValueSeriesCountsDepositsMadeBeforeTheRange()
    {
        var data = Data([Instrument(1, "IWDA")], cash: [Cash(CashKind.Deposit, Today.AddDays(-100), 300m), Cash(CashKind.Deposit, Today.AddDays(-1), 200m)]);

        var series = PortfolioMath.ValueSeries(data, [], PortfolioMath.CashNow(data), Today.AddDays(-7));

        Assert.Equal(8, series.Count);
        Assert.Equal(300m, series[0].Deposits);
        Assert.Equal(300m, series[0].Value);
        Assert.Equal(500m, series[^1].Deposits);
        Assert.Equal(500m, series[^1].Value);
    }

    [Fact]
    public void MoneyPutInIsNotCountedAsAGain()
    {
        var points = new List<ValuePoint>
        {
            new(Today.AddDays(-1), 1000m, 1000m),
            new(Today, 1600m, 1500m),
        };

        var change = PortfolioMath.RangeChange(points);

        Assert.NotNull(change);
        Assert.Equal(100m, change.Value.Change);
        Assert.Equal(100m / 1500m, change.Value.Pct);
        Assert.Null(PortfolioMath.RangeChange(points.Take(1).ToList()));
    }

    [Theory]
    [InlineData(ChartRange.Week, -7)]
    [InlineData(ChartRange.Month, -30)]
    [InlineData(ChartRange.All, -400)]
    public void RangesStartWhereTheyShouldButNeverBeforeTheFirstDay(ChartRange range, int expectedDays)
    {
        var firstDay = Today.AddDays(-400);

        var start = PortfolioMath.RangeStart(range, Today, firstDay);

        Assert.Equal(range == ChartRange.Month ? Today.AddMonths(-1) : Today.AddDays(expectedDays), start);
        Assert.Equal(Today.AddDays(-10), PortfolioMath.RangeStart(ChartRange.Year, Today, Today.AddDays(-10)));
        Assert.Equal(new DateOnly(Today.Year, 1, 1), PortfolioMath.RangeStart(ChartRange.YearToDate, Today, firstDay));
    }

    [Fact]
    public void ThinKeepsTheFirstAndTheLastPoint()
    {
        var points = Enumerable.Range(0, 1000).ToList();

        var thin = PortfolioMath.Thin(points, 100);

        Assert.Equal(100, thin.Count);
        Assert.Equal(0, thin[0]);
        Assert.Equal(999, thin[^1]);
        Assert.Equal(points.Take(50), PortfolioMath.Thin(points.Take(50).ToList(), 100));
    }

    [Fact]
    public void QuantityOnADayCountsTheTradesUpToThatDay()
    {
        var data = Data([Instrument(1, "IWDA")], trades: [Buy(1, Today.AddDays(-10), 4, 10m), Sell(1, Today.AddDays(-5), 1, 10m)]);

        Assert.Equal(0m, PortfolioMath.QuantityOn(data, 1, Today.AddDays(-11)));
        Assert.Equal(4m, PortfolioMath.QuantityOn(data, 1, Today.AddDays(-6)));
        Assert.Equal(3m, PortfolioMath.QuantityOn(data, 1, Today));
    }

    [Fact]
    public void TheFirstDayIsTheOldestThingThatHappened()
    {
        var data = Data(
            [Instrument(1, "IWDA")],
            trades: [Buy(1, Today.AddDays(-10), 1, 10m)],
            cash: [Cash(CashKind.Deposit, Today.AddDays(-12), 10m)]);

        Assert.Equal(Today.AddDays(-12), PortfolioMath.FirstDay(data));
        Assert.Equal(Today, PortfolioMath.FirstDay(Data([])));
    }
}
