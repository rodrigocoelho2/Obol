using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Obol.Sdk;
using Obol.Sdk.Models;
using Obol.Website.Model.Account;
using Obol.Website.Security;

namespace Obol.Website.Controllers;

/// <summary>
/// Sign in, create an account and sign out. Accounts are kept by the WebApi; passwords never reach the logs.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="options">Sign-in settings.</param>
/// <param name="throttle">Slows down password guessing.</param>
/// <param name="logger">Logger.</param>
[AllowAnonymous]
public sealed class AccountController(
    ObolClient apiClient,
    IOptions<WebsiteOptions> options,
    LoginThrottle throttle,
    ILogger<AccountController> logger) : Controller
{
    private const string Unreachable = "Can't reach Obol's engine right now. Check that the obol-webapi container is running, then try again.";
    private static readonly TimeSpan FailureDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>The sign-in form (the create-account form on the very first visit).</summary>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>The view or a redirect.</returns>
    [HttpGet("/sign-in")]
    public async Task<IActionResult> SignIn(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        var model = await PageModelAsync(returnUrl);
        if (model.IsFirstAccount)
        {
            return RedirectToAction(nameof(CreateAccount), new { returnUrl = model.ReturnUrl });
        }

        return View("SignIn", model);
    }

    /// <summary>Checks the user name and password and signs the browser in.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="remember">Keep the session on this device.</param>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>A redirect, or the form again with the error.</returns>
    [HttpPost("/sign-in")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignInAsync([FromForm] string? userName, [FromForm] string? password, [FromForm] bool remember, [FromForm] string? returnUrl)
    {
        var model = await PageModelAsync(returnUrl);
        model.UserName = (userName ?? string.Empty).Trim();
        model.Remember = remember;

        if (Throttled(model))
        {
            return View("SignIn", model);
        }

        try
        {
            var user = await apiClient.UserSignInAsync(new UserSignInRequest { UserName = model.UserName, Password = password });
            throttle.Succeeded(Address);
            await ObolAuthentication.SignInAsync(HttpContext, user, remember, options.Value.RememberDays);
            return LocalRedirect(model.ReturnUrl);
        }
        catch (ObolApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            throttle.Failed(Address);
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Failed sign-in for {UserName} from {Address}", model.UserName, Address);

            await Task.Delay(FailureDelay);
            model.Errors = ["Wrong user name or password."];
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return View("SignIn", model);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ObolApiException)
        {
            model.Errors = [Unreachable];
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("SignIn", model);
        }
    }

    /// <summary>The create-account form (always on the first visit; later only when registration is allowed).</summary>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>The view or a redirect.</returns>
    [HttpGet("/create-account")]
    public async Task<IActionResult> CreateAccount(string? returnUrl = null)
    {
        var model = await PageModelAsync(returnUrl);
        if (!model.CanCreateAccount && model.Unavailable is null)
        {
            return RedirectToAction(nameof(SignIn), new { returnUrl = model.ReturnUrl });
        }

        return View("CreateAccount", model);
    }

    /// <summary>Creates the account and signs the browser in.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="confirmPassword">The password again.</param>
    /// <param name="setupKey">OBOL_SETUP_KEY, for the first account.</param>
    /// <param name="remember">Keep the session on this device.</param>
    /// <param name="returnUrl">Where to go after signing in.</param>
    /// <returns>A redirect, or the form again with the errors.</returns>
    [HttpPost("/create-account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccountAsync(
        [FromForm] string? userName, [FromForm] string? password, [FromForm] string? confirmPassword, [FromForm] string? setupKey,
        [FromForm] bool remember, [FromForm] string? returnUrl)
    {
        var model = await PageModelAsync(returnUrl);
        model.UserName = (userName ?? string.Empty).Trim();
        model.Remember = remember;
        if (model.Unavailable is not null)
        {
            model.Errors = [model.Unavailable];
            return View("CreateAccount", model);
        }

        if (!model.CanCreateAccount)
        {
            return RedirectToAction(nameof(SignIn), new { returnUrl = model.ReturnUrl });
        }

        if (Throttled(model))
        {
            return View("CreateAccount", model);
        }

        if (model.NeedsSetupKey && !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(setupKey ?? string.Empty), Encoding.UTF8.GetBytes(options.Value.SetupKey)))
        {
            throttle.Failed(Address);
            await Task.Delay(FailureDelay);
            model.Errors = ["The setup key is wrong. It is OBOL_SETUP_KEY, in the .env file next to docker-compose.yml."];
            return View("CreateAccount", model);
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            model.Errors = ["The two passwords are different."];
            return View("CreateAccount", model);
        }

        try
        {
            var user = await apiClient.UserCreateAsync(new UserCreateRequest { UserName = model.UserName, Password = password });
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Account {UserName} created ({Role})", user.UserName, user.Role);

            await ObolAuthentication.SignInAsync(HttpContext, user, remember, options.Value.RememberDays);
            return LocalRedirect(model.ReturnUrl == "/" ? "/sync" : model.ReturnUrl);
        }
        catch (ObolApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            model.Errors = [.. exception.ToValidationErrors().Select(static error => error.Message)];
            return View("CreateAccount", model);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ObolApiException)
        {
            model.Errors = [Unreachable];
            return View("CreateAccount", model);
        }
    }

    /// <summary>Signs the browser out and shows the public page.</summary>
    /// <returns>A redirect to the start.</returns>
    [HttpPost("/sign-out")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/");
    }

    private string Address => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private bool Throttled(AccountPageModel model)
    {
        var wait = throttle.RetryAfter(Address);
        if (wait <= TimeSpan.Zero)
        {
            return false;
        }

        model.Errors = [$"Too many wrong attempts. Try again in {Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))} min."];
        Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return true;
    }

    private async Task<AccountPageModel> PageModelAsync(string? returnUrl)
    {
        var settings = options.Value;
        var model = new AccountPageModel { ReturnUrl = SafeReturnUrl(returnUrl), RememberDays = settings.RememberDays };
        try
        {
            var status = await apiClient.UserStatusAsync();
            model.IsFirstAccount = status.UserCount == 0;
            model.CanCreateAccount = model.IsFirstAccount || settings.AllowRegistration;
            model.NeedsSetupKey = model.IsFirstAccount && settings.RequiresSetupKey;
            model.DemoUserName = status.DemoUserName;
            model.DemoPassword = status.DemoPassword;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ObolApiException)
        {
            model.Unavailable = Unreachable;
        }

        return model;
    }

    private string SafeReturnUrl(string? url) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) && !url.StartsWith("/sign-", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("/create-account", StringComparison.OrdinalIgnoreCase) ? url : "/";
}
