using Microsoft.AspNetCore.Mvc;
using Obol.Sdk;
using Obol.Website.Model;
using Obol.Website.Security;

namespace Obol.Website.Controllers;

/// <summary>
/// Base of every signed-in Website controller. Controllers are thin: fill in the shell, create the model, call it, return its result.
/// </summary>
/// <param name="apiClient">The Obol.WebApi client handed to the models.</param>
[Microsoft.AspNetCore.Authorization.Authorize]
public abstract class WebsiteBaseController(ObolClient apiClient) : Controller
{
    /// <summary>Gets the API client.</summary>
    protected ObolClient ApiClient { get; } = apiClient;

    /// <summary>Gets the signed-in account's id.</summary>
    protected string UserId => ObolAuthentication.UserId(User);

    /// <summary>Gets the screen model.</summary>
    protected ScreenModel Screens => new(ApiClient);

    /// <summary>
    /// Fills in the shell of a page: who's signed in and the tab.
    /// </summary>
    /// <typeparam name="T">The page model.</typeparam>
    /// <param name="page">The page.</param>
    /// <param name="section">The tab.</param>
    /// <returns>The page.</returns>
    protected T Shell<T>(T page, AppSection section)
        where T : AppPageModel
    {
        page.UserName = User.Identity?.Name ?? string.Empty;
        page.IsAdmin = User.IsInRole("Admin");
        page.Section = section;
        return page;
    }

    /// <summary>
    /// The view of a screen, or the "engine isn't answering" card when its data couldn't be read (the screen's own view
    /// needs its data).
    /// </summary>
    /// <param name="viewName">The screen's view.</param>
    /// <param name="page">The filled page.</param>
    /// <returns>The view.</returns>
    protected ViewResult Screen(string viewName, AppPageModel page) =>
        View(page.Unavailable is null ? viewName : "~/Views/Shared/Unavailable.cshtml", page);
}
