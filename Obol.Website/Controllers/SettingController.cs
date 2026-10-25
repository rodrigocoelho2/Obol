using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Obol.Library.Validation;
using Obol.Sdk;
using Obol.Sdk.Models;
using Obol.Website.Model;
using Obol.Website.Security;

namespace Obol.Website.Controllers;

/// <summary>
/// Settings: the password, the accounts (admins), signing out.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="options">Sign-in settings.</param>
public sealed class SettingController(ObolClient apiClient, IOptions<WebsiteOptions> options) : WebsiteBaseController(apiClient)
{
    /// <summary>The Settings screen.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/settings")]
    public async Task<IActionResult> Index() => View("Index", await Screens.SettingsAsync(Shell(new SettingsPageModel(), AppSection.Settings), UserId));

    /// <summary>Changes the password (other devices are signed out; this one stays).</summary>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>Success, or the problems.</returns>
    [HttpPost("/settings/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Password([FromForm] string? currentPassword, [FromForm] string? newPassword)
    {
        try
        {
            var user = await ApiClient.UserPasswordAsync(new UserPasswordRequest { Id = UserId, CurrentPassword = currentPassword, NewPassword = newPassword });
            await ObolAuthentication.SignInAsync(HttpContext, user, true, options.Value.RememberDays);
            return Json(new AjaxResult());
        }
        catch (ObolApiException exception)
        {
            return Json(new AjaxResult { IsValid = false, ValidationErrors = [.. exception.ToValidationErrors()] });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Json(new AjaxResult { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, WebsiteBaseModel.ApiUnavailableMessage)] });
        }
    }

    /// <summary>Deletes an account and its portfolio (admins; or your own account).</summary>
    /// <param name="id">The account.</param>
    /// <returns>Success, or the problems.</returns>
    [HttpPost("/settings/delete-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser([FromForm] string? id)
    {
        var target = string.IsNullOrEmpty(id) ? UserId : id;
        if (target != UserId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            await ApiClient.UserDeleteAsync(target);
            if (target == UserId)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            return Json(new AjaxResult());
        }
        catch (ObolApiException exception)
        {
            return Json(new AjaxResult { IsValid = false, ValidationErrors = [.. exception.ToValidationErrors()] });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Json(new AjaxResult { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, WebsiteBaseModel.ApiUnavailableMessage)] });
        }
    }
}
