using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Obol.Library.Enums;
using Obol.Sdk;
using Obol.Website.Helpers;
using Obol.Website.Model;
using Obol.Website.Security;

namespace Obol.Website.Controllers;

/// <summary>
/// The Overview, and the public page that explains Obol.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="options">The sign-in settings (whether anyone may create an account).</param>
public sealed class DashboardController(ObolClient apiClient, IOptions<WebsiteOptions> options) : WebsiteBaseController(apiClient)
{
    /// <summary>
    /// Signed in: the Overview. Signed out: the public page with the way in.
    /// </summary>
    /// <param name="range">The chart range (1w, 1m, 3m, ytd, 1y, all).</param>
    /// <returns>The view.</returns>
    [HttpGet("/")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? range = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View("Landing", new LandingModel { CanCreateAccount = options.Value.AllowRegistration });
        }

        var page = Shell(new OverviewPageModel { Range = Fmt.ParseRange(range) }, AppSection.Overview);
        return Screen("Index", await Screens.OverviewAsync(page, UserId));
    }

    /// <summary>
    /// The public page that explains Obol (also reachable when signed in).
    /// </summary>
    /// <returns>The view.</returns>
    [HttpGet("/about")]
    [AllowAnonymous]
    public IActionResult Landing() => View("Landing", new LandingModel
    {
        SignedIn = User.Identity?.IsAuthenticated == true,
        CanCreateAccount = options.Value.AllowRegistration,
    });
}

/// <summary>
/// Holdings, and one position.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class HoldingController(ObolClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>The open positions.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/holdings")]
    public async Task<IActionResult> Index() =>
        Screen("Index", await Screens.HoldingsAsync(Shell(new HoldingsPageModel(), AppSection.Holdings), UserId));

    /// <summary>One position (or an instrument held before).</summary>
    /// <param name="key">The instrument key.</param>
    /// <param name="range">The chart range.</param>
    /// <returns>The view, or 404.</returns>
    [HttpGet("/holdings/{key}")]
    public async Task<IActionResult> Position(string key, string? range = null)
    {
        var page = Shell(new PositionPageModel { Key = key, Range = Fmt.ParseRange(range) }, AppSection.Holdings);
        page = await Screens.PositionAsync(page, UserId);
        if (page.Data is null && page.Unavailable is null)
        {
            return NotFound();
        }

        return Screen("Position", page);
    }
}

/// <summary>
/// Dividends and interest.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class IncomeController(ObolClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>The Income screen.</summary>
    /// <param name="year">The year (this one when empty).</param>
    /// <returns>The view.</returns>
    [HttpGet("/income")]
    public async Task<IActionResult> Index(int? year = null) =>
        Screen("Index", await Screens.IncomeAsync(Shell(new IncomePageModel(), AppSection.Income), UserId, year));
}

/// <summary>
/// Every trade, dividend and transfer.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class ActivityController(ObolClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>The Activity screen.</summary>
    /// <param name="kind">Only one kind (buy, sell, dividend…).</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The view.</returns>
    [HttpGet("/activity")]
    public async Task<IActionResult> Index(string? kind = null, int page = 1)
    {
        var model = Shell(new ActivityPageModel
        {
            Kind = Enum.TryParse<ActivityKind>(kind, ignoreCase: true, out var only) ? only : null,
            Page = Math.Max(1, page),
        }, AppSection.Activity);
        return Screen("Index", await Screens.ActivityAsync(model, UserId));
    }
}

/// <summary>
/// Company and fund logos, from the WebApi (which keeps them), so the phone never asks a third party.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class LogoController(ObolClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>The logo of an instrument; 404 when there is none (the page shows the ticker badge).</summary>
    /// <param name="key">The instrument key.</param>
    /// <param name="s">The symbol.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The image.</returns>
    [HttpGet("/logo/{key}")]
    public async Task<IActionResult> Get(string key, string? s, CancellationToken cancellationToken)
    {
        try
        {
            var logo = await ApiClient.LogoGetAsync(key, s, cancellationToken);
            if (logo is not { } image)
            {
                Response.Headers.CacheControl = "private, max-age=86400";
                return NotFound();
            }

            Response.Headers.CacheControl = "private, max-age=2592000";
            return File(image.Data, image.ContentType);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ObolApiException)
        {
            return NotFound();
        }
    }
}
