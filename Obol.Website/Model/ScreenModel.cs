using Obol.Library.Enums;
using Obol.Sdk;

namespace Obol.Website.Model;

/// <summary>
/// Fills the screens from the WebApi. A screen whose data can't be read shows <see cref="AppPageModel.Unavailable"/>
/// instead (the shell stays).
/// </summary>
/// <param name="apiClient">The Obol.WebApi client.</param>
public sealed class ScreenModel(ObolClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Fills the Overview.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<OverviewPageModel> OverviewAsync(OverviewPageModel page, string userId)
    {
        var result = await CallAsync(() => ApiClient.PortfolioOverviewAsync(userId, page.Range));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills Holdings.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<HoldingsPageModel> HoldingsAsync(HoldingsPageModel page, string userId)
    {
        var result = await CallAsync(() => ApiClient.PortfolioHoldingsAsync(userId));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills a position.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page, with no data when the instrument is unknown.</returns>
    public async Task<PositionPageModel> PositionAsync(PositionPageModel page, string userId)
    {
        var result = await CallAsync(() => ApiClient.PortfolioPositionAsync(userId, page.Key, page.Range));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result, notFoundIsEmpty: true);
    }

    /// <summary>Fills Income.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <param name="year">The year asked for.</param>
    /// <returns>The page.</returns>
    public async Task<IncomePageModel> IncomeAsync(IncomePageModel page, string userId, int? year)
    {
        var result = await CallAsync(() => ApiClient.PortfolioIncomeAsync(userId, year));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills Activity.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<ActivityPageModel> ActivityAsync(ActivityPageModel page, string userId)
    {
        var skip = (Math.Max(1, page.Page) - 1) * ActivityPageModel.PageSize;
        var result = await CallAsync(() => ApiClient.PortfolioActivityAsync(userId, page.Kind, skip, ActivityPageModel.PageSize));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills Insights › Performance.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<PerformancePageModel> PerformanceAsync(PerformancePageModel page, string userId)
    {
        var result = await CallAsync(() => ApiClient.InsightsPerformanceAsync(userId, page.Range));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills Insights › Exposure.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<ExposurePageModel> ExposureAsync(ExposurePageModel page, string userId)
    {
        var result = await CallAsync(() => ApiClient.InsightsExposureAsync(userId));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills Insights › Realized.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <param name="year">The year.</param>
    /// <returns>The page.</returns>
    public async Task<RealizedPageModel> RealizedAsync(RealizedPageModel page, string userId, int? year)
    {
        var result = await CallAsync(() => ApiClient.InsightsRealizedAsync(userId, year));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills Insights › Goals.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<GoalsPageModel> GoalsAsync(GoalsPageModel page, string userId)
    {
        var result = await CallAsync(() => ApiClient.InsightsGoalsAsync(userId));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills Insights › Coach.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<CoachPageModel> CoachAsync(CoachPageModel page, string userId)
    {
        var result = await CallAsync(() => ApiClient.InsightsCoachAsync(userId));
        page.Data = result.Data;
        return await ShellAsync(page, userId, result);
    }

    /// <summary>Fills Sync.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<SyncPageModel> SyncAsync(SyncPageModel page, string userId)
    {
        var result = await CallAsync(() => ApiClient.ConnectionGetAsync(userId));
        page.Data = result.Data;
        page.Connection = result.Data;
        if (!result.IsValid)
        {
            page.Unavailable = result.ValidationErrors.FirstOrDefault()?.Message ?? ApiUnavailableMessage;
        }

        return page;
    }

    /// <summary>Fills Settings.</summary>
    /// <param name="page">The page.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<SettingsPageModel> SettingsAsync(SettingsPageModel page, string userId)
    {
        page.UserId = userId;
        if (page.IsAdmin)
        {
            var users = await CallAsync(() => ApiClient.UserListAsync());
            page.Users = users.Data ?? [];
        }

        var connection = await CallAsync(() => ApiClient.ConnectionGetAsync(userId));
        page.Connection = connection.Data;
        return page;
    }

    private async Task<T> ShellAsync<T, TData>(T page, string userId, AjaxDataResult<TData> result, bool notFoundIsEmpty = false)
        where T : AppPageModel
    {
        if (!result.IsValid && !(notFoundIsEmpty && result.ValidationErrors.Count > 0 && !result.ValidationErrors[0].Message.StartsWith("Can't reach", StringComparison.Ordinal)))
        {
            page.Unavailable = result.ValidationErrors.FirstOrDefault()?.Message ?? ApiUnavailableMessage;
            return page;
        }

        var connection = await CallAsync(() => ApiClient.ConnectionGetAsync(userId));
        page.Connection = connection.Data;
        return page;
    }

    /// <summary>The chart ranges, in the order the tabs show them.</summary>
    public static readonly ChartRange[] Ranges = [ChartRange.Week, ChartRange.Month, ChartRange.Quarter, ChartRange.YearToDate, ChartRange.Year, ChartRange.All];
}
