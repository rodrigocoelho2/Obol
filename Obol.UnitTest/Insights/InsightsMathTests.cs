using Obol.Library.Enums;
using Obol.WebApi.Features.Csv;
using Obol.WebApi.Features.Insights.Services;
using Obol.WebApi.Features.Portfolio.Services;
using static Obol.UnitTest.TestSupport;

namespace Obol.UnitTest.Insights;

/// <summary>
/// The maths of the Insights screens: money- and time-weighted returns, the same money in an index, monthly returns,
/// realized gains, countries, exchange-rate effects and the income by position.
/// </summary>
public sealed class InsightsMathTests
{
    private static readonly DateOnly Start = new(2025, 1, 1);

    [Fact]
    public void OneDepositThatGrewTenPercentInAYearIsTenPercentAYear()
    {
        var xirr = InsightsMath.Xirr([new CashFlow(Start, 1000m)], Start.AddDays(365), 1100m);

        Assert.NotNull(xirr);
        Assert.InRange(xirr.Value, 0.0999m, 0.1001m);
    }

    [Fact]
    public void MoneyPutInLaterCountsForLess()
    {
        var xirr = InsightsMath.Xirr([new CashFlow(Start, 1000m), new CashFlow(Start.AddDays(183), 1000m)], Start.AddDays(365), 2100m);

        Assert.NotNull(xirr);
        Assert.InRange(xirr.Value, 0.06m, 0.075m);
    }

    [Fact]
    public void WithdrawalsAndLossesWork()
    {
        var loss = InsightsMath.Xirr([new CashFlow(Start, 1000m)], Start.AddDays(365), 800m);
        var withdrawal = InsightsMath.Xirr([new CashFlow(Start, 1000m), new CashFlow(Start.AddDays(100), -500m)], Start.AddDays(365), 600m);

        Assert.NotNull(loss);
        Assert.InRange(loss.Value, -0.2001m, -0.1999m);
        Assert.NotNull(withdrawal);
        Assert.True(withdrawal > 0);
    }

    [Fact]
    public void NothingPutInHasNoReturn()
    {
        Assert.Null(InsightsMath.Xirr([], Start.AddDays(10), 100m));
        Assert.Null(InsightsMath.Xirr([new CashFlow(Start, -100m)], Start.AddDays(10), 100m));
        Assert.Null(InsightsMath.Xirr([new CashFlow(Start, 100m)], Start, 100m));
    }

    [Fact]
    public void TimeWeightedTakesEachDaysDepositsOut()
    {
        var values = new List<(DateOnly, decimal)> { (Start, 100m), (Start.AddDays(1), 110m), (Start.AddDays(2), 220m) };
        var flows = new Dictionary<DateOnly, decimal> { [Start.AddDays(2)] = 100m };

        var index = InsightsMath.TimeWeighted(values, flows);

        Assert.Equal(1m, index[0].Index);
        Assert.Equal(1.1m, index[1].Index);
        Assert.InRange(index[2].Index, 1.1999m, 1.2001m);
    }

    [Fact]
    public void TheSameMoneyInAnIndexBuysUnitsAtEachDaysClose()
    {
        var days = new[] { Start, Start.AddDays(1), Start.AddDays(2) };
        var prices = new Dictionary<DateOnly, decimal> { [days[0]] = 10m, [days[1]] = 11m, [days[2]] = 22m };
        var flows = new Dictionary<DateOnly, decimal> { [days[2]] = 110m };

        var values = InsightsMath.SameMoneyIn(days, 100m, flows, day => prices[day]);

        Assert.NotNull(values);
        Assert.Equal(new[] { 100m, 110m, 330m }, values);
        Assert.Null(InsightsMath.SameMoneyIn(days, 100m, flows, static _ => null));
    }

    [Fact]
    public void MonthlyReturnsChainFromTheEndOfEachMonth()
    {
        var index = new List<(DateOnly, decimal)>
        {
            (new DateOnly(2025, 1, 15), 1m), (new DateOnly(2025, 1, 31), 1.1m), (new DateOnly(2025, 2, 28), 1.21m), (new DateOnly(2025, 3, 3), 1.089m),
        };

        var months = InsightsMath.Monthly(index);

        Assert.Equal(3, months.Count);
        Assert.Equal((2025, 1, 0.1m), months[0]);
        Assert.Equal((2025, 2, 0.1m), months[1]);
        Assert.Equal((2025, 3, -0.1m), months[2]);
    }

    [Fact]
    public void SalesUseTheAverageCostOrTradingTwelvesResult()
    {
        var trades = new[]
        {
            Buy(1, Start, 10, 10m),
            Buy(1, Start.AddDays(10), 10, 20m),
            Sell(1, Start.AddDays(20), 5, 25m),
            Sell(2, Start.AddDays(30), 2, 50m, total: 100m),
        };
        trades[3].RealizedPL = 30m;

        var sales = InsightsMath.Sales(trades);

        Assert.Equal(2, sales.Count);
        Assert.Equal(75m, sales[0].Cost);
        Assert.Equal(50m, sales[0].Gain);
        Assert.Equal(70m, sales[1].Cost);
        Assert.Equal(30m, sales[1].Gain);
    }

    [Fact]
    public void ASaleWithoutItsBuysHasNoKnownGain()
    {
        var sale = Assert.Single(InsightsMath.Sales([Sell(1, Start, 1, 10m)]));

        Assert.Null(sale.Cost);
        Assert.Null(sale.Gain);
    }

    [Theory]
    [InlineData("US0378331005", "US", "United States")]
    [InlineData("PTGAL0AM0009", "PT", "Portugal")]
    [InlineData("MHY410531021", "MH", "Marshall Islands")]
    [InlineData("XX0000000000", "XX", "XX")]
    [InlineData(null, "", "Unknown")]
    public void CountriesComeFromTheIsin(string? isin, string code, string name) =>
        Assert.Equal((code, name), InsightsMath.Country(isin));

    [Fact]
    public void TheExchangeRatesEffectIsWorkedOutFromCostAndPrice()
    {
        var prices = new PriceBook("EUR");
        prices.AddPrice(2, Today, 110m);
        prices.AddFx("USD", Today, 0.8m);
        var data = Data([Instrument(2, "AAPL", "USD", AssetClass.Stock)], trades: [Buy(2, Today.AddDays(-100), 10, 100m, total: 900m, currency: "USD")], prices: prices);
        var position = Assert.Single(PortfolioMath.Positions(data));

        var effect = InsightsService.FxImpact(position, data);

        Assert.Equal(10m * 110m * (0.8m - 0.9m), effect);
    }

    [Fact]
    public void ExposureGroupsCurrenciesSectorsAndCountries()
    {
        var apple = Instrument(2, "AAPL", "USD", AssetClass.Stock);
        apple.Isin = "US0378331005";
        apple.Sector = "Technology";
        var data = Data(
            [Instrument(1, "IWDA"), apple],
            trades: [Buy(1, Today.AddDays(-50), 10, 30m), Buy(2, Today.AddDays(-50), 1, 100m, total: 100m, currency: "USD")],
            cash: [Cash(CashKind.Deposit, Today.AddDays(-60), 500m)]);

        var exposure = InsightsService.Exposure(data);

        Assert.Equal(new[] { "EUR", "USD" }, exposure.Currencies.Select(static slice => slice.Key));
        Assert.Equal(300m + 100m, exposure.Currencies[0].Value);
        Assert.Equal(new[] { "Equity ETFs", "Technology" }, exposure.Sectors.Select(static slice => slice.Label));
        Assert.Contains(exposure.Countries, static slice => slice.Key == "US");
        Assert.Contains(exposure.Countries, static slice => slice.Key == "FUND");
        Assert.Equal(2, exposure.Concentration.Positions);
        Assert.Equal(0.75m, exposure.Concentration.TopWeight);
        Assert.InRange(exposure.Concentration.Effective, 1.5m, 1.7m);
    }

    [Fact]
    public void RaisesAndCutsAreSpottedPerPosition()
    {
        var data = Data(
            [Instrument(1, "VHYL"), Instrument(2, "GALP", assetClass: AssetClass.Stock)],
            trades: [Buy(1, Today.AddDays(-300), 10, 60m), Buy(2, Today.AddDays(-300), 10, 15m)],
            dividends:
            [
                PerShare(Dividend(1, Today.AddDays(-200), 4m, 10m), 0.40m), PerShare(Dividend(1, Today.AddDays(-20), 4.4m, 10m), 0.44m),
                PerShare(Dividend(2, Today.AddDays(-200), 3m, 10m), 0.30m), PerShare(Dividend(2, Today.AddDays(-20), 2m, 10m), 0.20m),
            ]);

        var income = IncomeMath.ByPosition(data, PortfolioMath.Positions(data));

        Assert.Equal("raised", income.Single(static item => item.InstrumentId == 1).Trend);
        Assert.Equal("cut", income.Single(static item => item.InstrumentId == 2).Trend);
        Assert.Equal(8.4m, income.Single(static item => item.InstrumentId == 1).Received);
        Assert.Equal(2, income.Single(static item => item.InstrumentId == 1).Payments);
    }

    [Fact]
    public void WithholdingInDollarsIsTurnedIntoEurosAtThePaymentsRate()
    {
        Assert.Equal(0.04m, Trading212Csv.Withheld(0.05m, "USD", "EUR", "USD", 0.23m, 3.1234003100m, 0.087125m));
        Assert.Equal(0.10m, Trading212Csv.Withheld(0.10m, "EUR", "EUR", "USD", 1m, 1m, 1m));
        Assert.Null(Trading212Csv.Withheld(null, "USD", "EUR", "USD", 1m, 1m, 1m));
        Assert.Null(Trading212Csv.Withheld(0.1m, "GBP", "EUR", "USD", 1m, 1m, 1m));
    }

    [Fact]
    public void TheCoachsAnswerIsReadEvenAroundExtraText()
    {
        var report = CoachPrompt.Parse("Here you go:\n{\"headline\": \"Too much in one name\", \"summary\": \"S\", \"strengths\": [\"a\"], \"watch\": [\"b\", \"c\"], \"actions\": [\"1\", \"2\", \"3\"]}\nThanks");
        var plain = CoachPrompt.Parse("Just text");

        Assert.Equal("Too much in one name", report.Headline);
        Assert.Equal(2, report.Watch.Count);
        Assert.Equal(3, report.Actions.Count);
        Assert.Equal("Just text", plain.Summary);
    }

    private static Obol.WebApi.Features.Portfolio.Models.Entities.DividendRow PerShare(Obol.WebApi.Features.Portfolio.Models.Entities.DividendRow dividend, decimal perShare)
    {
        dividend.PerShare = perShare;
        return dividend;
    }
}
