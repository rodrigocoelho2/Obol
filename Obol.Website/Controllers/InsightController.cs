using Microsoft.AspNetCore.Mvc;
using Obol.Library.Validation;
using Obol.Sdk;
using Obol.Sdk.Models;
using Obol.Website.Helpers;
using Obol.Website.Model;

namespace Obol.Website.Controllers;

/// <summary>
/// The Insights tab: Performance, Exposure, Realized, Goals and the AI coach.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class InsightController(ObolClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Insights › Performance: returns against MSCI World and the S&amp;P 500.</summary>
    /// <param name="range">The range (1w, 1m, 3m, ytd, 1y, all).</param>
    /// <returns>The view.</returns>
    [HttpGet("/insights")]
    public async Task<IActionResult> Performance(string? range = null) =>
        Screen("Performance", await Screens.PerformanceAsync(Shell(new PerformancePageModel { Range = Fmt.ParseRange(range) }, AppSection.Insights), UserId));

    /// <summary>Insights › Exposure: currencies, sectors, countries and concentration.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/insights/exposure")]
    public async Task<IActionResult> Exposure() =>
        Screen("Exposure", await Screens.ExposureAsync(Shell(new ExposurePageModel(), AppSection.Insights), UserId));

    /// <summary>Insights › Realized: what the sales made, by year.</summary>
    /// <param name="year">The year.</param>
    /// <returns>The view.</returns>
    [HttpGet("/insights/realized")]
    public async Task<IActionResult> Realized(int? year = null) =>
        Screen("Realized", await Screens.RealizedAsync(Shell(new RealizedPageModel(), AppSection.Insights), UserId, year));

    /// <summary>Insights › Goals: where the money could be in a few years.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/insights/goals")]
    public async Task<IActionResult> Goals() =>
        Screen("Goals", await Screens.GoalsAsync(Shell(new GoalsPageModel(), AppSection.Insights), UserId));

    /// <summary>Insights › Coach: the AI's review of the portfolio.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/insights/coach")]
    public async Task<IActionResult> Coach() =>
        Screen("Coach", await Screens.CoachAsync(Shell(new CoachPageModel(), AppSection.Insights), UserId));

    /// <summary>The coach, for the page to follow a running review.</summary>
    /// <returns>The coach.</returns>
    [HttpGet("/insights/coach/status")]
    public async Task<IActionResult> CoachStatus() => Json(await Call(() => ApiClient.InsightsCoachAsync(UserId)));

    /// <summary>Starts a new review.</summary>
    /// <returns>The coach, running.</returns>
    [HttpPost("/insights/coach/run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CoachRun() => Json(await Call(() => ApiClient.InsightsCoachAnalyseAsync(UserId)));

    private static async Task<AjaxDataResult<T>> Call<T>(Func<Task<T>> call)
    {
        try
        {
            return new AjaxDataResult<T> { Data = await call() };
        }
        catch (ObolApiException exception)
        {
            return new AjaxDataResult<T> { IsValid = false, ValidationErrors = [.. exception.ToValidationErrors()] };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new AjaxDataResult<T> { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, WebsiteBaseModel.ApiUnavailableMessage)] };
        }
    }
}
