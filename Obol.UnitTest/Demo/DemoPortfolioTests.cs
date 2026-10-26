using Obol.Library.Enums;

namespace Obol.UnitTest.Demo;

/// <summary>
/// The whole WebApi on the demo account: every screen builds and its numbers agree with each other.
/// </summary>
public sealed class DemoPortfolioTests
{
    private const string WorldEtf = "IE00B4L5Y983";

    [Fact]
    public async Task TheOverviewAddsUp()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var response = await app.PortfolioService.OverviewAsync(userId, ChartRange.Year);

        Assert.True(response.IsSuccess);
        var overview = response.Result!;
        Assert.True(overview.HasData);
        Assert.True(overview.Connected);
        Assert.Equal("EUR", overview.Currency);
        Assert.Equal(6, overview.Positions.Count);
        Assert.Equal(overview.Positions.Sum(static position => position.Value) + overview.Cash, overview.TotalValue);
        Assert.True(overview.TotalValue > 0);
        Assert.NotNull(overview.NetDeposits);
        Assert.Equal(overview.TotalValue - overview.NetDeposits, overview.TotalReturn);
        Assert.InRange(overview.Allocation.Sum(static slice => slice.Percent), 0.999m, 1.001m);
        Assert.InRange(overview.Positions.Sum(static position => position.Weight), 0.999m, 1.001m);
        Assert.NotEmpty(overview.Series);
        Assert.True(overview.Series.Count <= 240);
        Assert.Equal(TestSupport.Today, overview.Series[^1].Day);
        Assert.InRange(overview.Series[^1].Value, overview.TotalValue - 1m, overview.TotalValue + 1m);
        Assert.All(overview.Series, static point => Assert.True(point.Value >= 0));
    }

    [Fact]
    public async Task TheOverviewGroupsThePiesLikeTrading212()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var overview = (await app.PortfolioService.OverviewAsync(userId, ChartRange.Year)).Result!;

        Assert.Equal(new[] { "Big tech", "Long term" }, overview.Pies.Select(static pie => pie.Name).Order());
        var tech = overview.Pies.Single(static pie => pie.Name == "Big tech");
        Assert.Equal(2, tech.Holdings.Count);
        Assert.Equal(tech.Holdings.Sum(static holding => holding.Value), tech.Value);
        Assert.Equal(tech.Value - tech.Invested, tech.Gain);
        // Outside the pies: the dividend ETF and Galp in full, and what's left of the world ETF and Apple.
        Assert.Equal(4, overview.OutsidePies.Count);
        var world = overview.OutsidePies.Single(static slice => slice.Instrument.Key == WorldEtf);
        var worldPosition = overview.Positions.Single(static position => position.Instrument.Key == WorldEtf);
        Assert.Equal(worldPosition.Quantity * 0.2m, world.Quantity, 6);
        // Every position is counted once, in a pie or outside.
        var positions = overview.Positions.Sum(static position => position.Value);
        Assert.InRange(overview.Pies.Sum(static pie => pie.Value) + overview.OutsidePies.Sum(static slice => slice.Value), positions - 0.01m, positions + 0.01m);
        Assert.InRange(overview.Pies.Sum(static pie => pie.Weight) + overview.OutsidePies.Sum(static slice => slice.Weight), 0.999m, 1.001m);
    }

    [Theory]
    [InlineData(ChartRange.Week)]
    [InlineData(ChartRange.Month)]
    [InlineData(ChartRange.Quarter)]
    [InlineData(ChartRange.YearToDate)]
    [InlineData(ChartRange.Year)]
    [InlineData(ChartRange.All)]
    public async Task EveryRangeHasAChart(ChartRange range)
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var overview = (await app.PortfolioService.OverviewAsync(userId, range)).Result!;

        Assert.Equal(range, overview.Range);
        Assert.True(overview.Series.Count >= 2);
        Assert.NotNull(overview.RangeChange);
        Assert.True(overview.Series.Zip(overview.Series.Skip(1)).All(static pair => pair.First.Day < pair.Second.Day));
    }

    [Fact]
    public async Task HoldingsMatchTheOverview()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var overview = (await app.PortfolioService.OverviewAsync(userId, ChartRange.Month)).Result!;
        var holdings = (await app.PortfolioService.HoldingsAsync(userId)).Result!;

        Assert.Equal(overview.TotalValue - overview.Cash, holdings.Value);
        Assert.Equal(overview.Cash, holdings.Cash);
        Assert.Equal(holdings.Positions.OrderByDescending(static position => position.Value).Select(static position => position.Instrument.Key), holdings.Positions.Select(static position => position.Instrument.Key));
        Assert.Contains(holdings.Positions, static position => position.Instrument.Class == AssetClass.BondEtf);
        Assert.Contains(holdings.Positions, static position => position.Instrument.Currency == "USD");
    }

    [Fact]
    public async Task APositionHasItsPricesTradesAndDividends()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var response = await app.PortfolioService.PositionAsync(userId, WorldEtf, ChartRange.Year);

        Assert.True(response.IsSuccess);
        var detail = response.Result!;
        Assert.Equal("IWDA", detail.Instrument.Symbol);
        Assert.NotNull(detail.Position);
        Assert.NotEmpty(detail.Prices);
        Assert.NotEmpty(detail.Marks);
        Assert.NotEmpty(detail.Activity);
        Assert.True(detail.Invested > 0);
        Assert.False((await app.PortfolioService.PositionAsync(userId, "NOPE", ChartRange.Year)).IsSuccess);
    }

    [Fact]
    public async Task IncomeHasThisYearsPaymentsAndWhatIsExpected()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var income = (await app.PortfolioService.IncomeAsync(userId, null)).Result!;

        Assert.Equal(TestSupport.Today.Year, income.Year);
        Assert.True(income.IsCurrentYear);
        Assert.Equal(12, income.Months.Count);
        Assert.True(income.Received > 0);
        Assert.True(income.Next12 > 0);
        Assert.Equal(income.Received, income.Months.Sum(static month => month.Received));
        Assert.Contains(TestSupport.Today.Year, income.Years);
        Assert.NotEmpty(income.Sources);
    }

    [Fact]
    public async Task TheActivityPagesThroughEverything()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var first = (await app.PortfolioService.ActivityAsync(userId, null, 0, 10)).Result!;
        var second = (await app.PortfolioService.ActivityAsync(userId, null, 10, 10)).Result!;
        var buys = (await app.PortfolioService.ActivityAsync(userId, ActivityKind.Buy, 0, 500)).Result!;

        Assert.True(first.Total > 20);
        Assert.Equal(10, first.Items.Count);
        Assert.True(first.Items.Zip(first.Items.Skip(1)).All(static pair => pair.First.At >= pair.Second.At));
        Assert.True(first.Items[^1].At >= second.Items[0].At);
        Assert.All(buys.Items, static item => Assert.Equal(ActivityKind.Buy, item.Kind));
        Assert.True(buys.Total < first.Total);
    }

    [Fact]
    public async Task AnEmptyAccountHasEmptyScreens()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();

        var overview = (await app.PortfolioService.OverviewAsync(userId, ChartRange.Month)).Result!;
        var income = (await app.PortfolioService.IncomeAsync(userId, null)).Result!;

        Assert.False(overview.HasData);
        Assert.False(overview.Connected);
        Assert.Equal(0m, overview.TotalValue);
        Assert.Empty(overview.Positions);
        Assert.Equal(0m, income.Received);
        Assert.False((await app.PortfolioService.OverviewAsync("not-a-guid", ChartRange.Month)).IsSuccess);
    }
}
