using Obol.Library.Enums;
using Obol.WebApi.Features.Insights.Services;

namespace Obol.UnitTest.Insights;

/// <summary>
/// The Insights screens on the demo account: everything builds and agrees with the rest of the app.
/// </summary>
public sealed class DemoInsightsTests
{
    [Theory]
    [InlineData(ChartRange.Month)]
    [InlineData(ChartRange.Year)]
    [InlineData(ChartRange.All)]
    public async Task PerformanceComparesWithBothIndexes(ChartRange range)
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var performance = (await app.Insights.PerformanceAsync(userId, range)).Result!;

        Assert.True(performance.HasData);
        Assert.True(performance.FromTransfers);
        Assert.NotNull(performance.TimeWeighted);
        Assert.NotNull(performance.MoneyWeighted);
        Assert.Equal(2, performance.Benchmarks.Count);
        Assert.All(performance.Benchmarks, static benchmark => Assert.True(benchmark.Available));
        Assert.True(performance.Series.Count > 2);
        Assert.All(performance.Series, point => Assert.Equal(2, point.Benchmarks.Count));
        Assert.Equal(performance.Value - performance.Invested, performance.Gain);
        Assert.NotEmpty(performance.Months);
    }

    [Fact]
    public async Task ThePerformanceValueIsTheNetWorth()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var performance = (await app.Insights.PerformanceAsync(userId, ChartRange.All)).Result!;
        var overview = (await app.PortfolioService.OverviewAsync(userId, ChartRange.All)).Result!;

        Assert.InRange(performance.Value, overview.TotalValue - 1m, overview.TotalValue + 1m);
        Assert.InRange(performance.Invested, overview.NetDeposits!.Value - 1m, overview.NetDeposits.Value + 1m);
        Assert.NotNull(performance.MoneyWeightedYearly);
    }

    [Fact]
    public async Task ExposureRealizedAndGoalsBuild()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var exposure = (await app.Insights.ExposureAsync(userId)).Result!;
        var realized = (await app.Insights.RealizedAsync(userId, null)).Result!;
        var goals = (await app.Insights.GoalsAsync(userId)).Result!;

        Assert.Contains(exposure.Currencies, static slice => slice.Key == "USD" && slice.FxImpact is not null);
        Assert.Contains(exposure.Sectors, static slice => slice.Label == "Technology");
        Assert.InRange(exposure.Currencies.Sum(static slice => slice.Percent), 0.999m, 1.001m);
        Assert.True(realized.ByYear.Count >= 2);
        Assert.Equal(realized.Sales.Count, realized.Wins + realized.Losses + realized.Unknown);
        Assert.Equal(3, goals.Scenarios.Count);
        Assert.True(goals.MonthlyAverage > 0);
        Assert.True(goals.Value > 0);
    }

    [Fact]
    public async Task IncomeHasEveryPayingPosition()
    {
        var app = new TestApp(demo: true);
        var userId = await app.CreateUserAsync("demo");

        var income = (await app.PortfolioService.IncomeAsync(userId, null)).Result!;

        Assert.NotEmpty(income.Positions);
        Assert.True(income.Withheld > 0);
        Assert.All(income.Positions, static item => Assert.True(item.Received >= 0));
    }

    [Fact]
    public void TheDemosExampleReviewHasEverySection()
    {
        var report = CoachService.Example(TestSupport.Now);

        Assert.True(report.Example);
        Assert.NotEmpty(report.Headline);
        Assert.Equal(3, report.Strengths.Count);
        Assert.Equal(3, report.Actions.Count);
    }
}
