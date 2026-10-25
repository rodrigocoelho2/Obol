using System.Globalization;
using System.Security.Claims;
using Obol.Sdk;
using Obol.Sdk.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace Obol.Website.Security;

/// <summary>
/// Sign-in with an account (user name + password, kept by the WebApi) and a cookie. Every few minutes the cookie is
/// checked against the account, so a deleted account or a changed password ends the other sessions.
/// </summary>
internal static class ObolAuthentication
{
    /// <summary>The sign-in page.</summary>
    public const string SignInPath = "/sign-in";

    /// <summary>The create-account page.</summary>
    public const string CreateAccountPath = "/create-account";

    /// <summary>The name of the session cookie.</summary>
    public const string CookieName = "obol.session";

    /// <summary>The claim holding the account's security stamp.</summary>
    public const string StampClaim = "obol:stamp";

    private const string CheckedAtKey = "obol:checked";
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Registers cookie authentication, the sign-in throttle and (optionally) persistent cookie keys.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddObolAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(WebsiteOptions.SectionName).Get<WebsiteOptions>() ?? new WebsiteOptions();

        var dataProtection = services.AddDataProtection().SetApplicationName("Obol");
        if (!string.IsNullOrWhiteSpace(options.KeysDirectory))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.KeysDirectory));
        }

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LoginThrottle>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // the NAS is usually plain HTTP on the LAN
                cookie.LoginPath = SignInPath;
                cookie.ReturnUrlParameter = "returnUrl";
                cookie.SlidingExpiration = true;
                cookie.ExpireTimeSpan = TimeSpan.FromDays(Math.Clamp(options.RememberDays, 1, 365));
                cookie.Events.OnRedirectToLogin = context =>
                {
                    // Pages go to the sign-in form; the JSON actions get a plain 401.
                    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        // Same-site path only: an absolute http:// address behind an HTTPS proxy would leave the installed app.
                        context.Response.Redirect(Uri.TryCreate(context.RedirectUri, UriKind.Absolute, out var absolute) ? absolute.PathAndQuery : context.RedirectUri);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                cookie.Events.OnValidatePrincipal = ValidateAsync;
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// The principal of an account.
    /// </summary>
    /// <param name="user">The account.</param>
    /// <returns>The principal stored in the cookie.</returns>
    public static ClaimsPrincipal Principal(UserDTO user) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Name, user.UserName),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim(StampClaim, user.SecurityStamp),
    ], CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>
    /// Signs the browser in as an account.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="user">The account.</param>
    /// <param name="remember">Keep the session after the browser closes.</param>
    /// <param name="rememberDays">For how long, when remembered.</param>
    /// <returns>A task that completes when the cookie is set.</returns>
    public static Task SignInAsync(HttpContext context, UserDTO user, bool remember, int rememberDays)
    {
        var properties = new AuthenticationProperties
        {
            IsPersistent = remember,
            ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(Math.Clamp(rememberDays, 1, 365)) : null,
            AllowRefresh = true,
        };
        properties.Items[CheckedAtKey] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Principal(user), properties);
    }

    /// <summary>The account id of the signed-in user, or empty.</summary>
    /// <param name="user">The principal.</param>
    /// <returns>The id.</returns>
    public static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>
    /// Every <see cref="CheckEvery"/>, makes sure the account still exists and its password did not change.
    /// When the WebApi cannot be reached, the session is kept (the pages show the connection problem instead).
    /// </summary>
    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var now = DateTimeOffset.UtcNow;
        if (context.Properties.Items.TryGetValue(CheckedAtKey, out var checkedAt)
            && long.TryParse(checkedAt, CultureInfo.InvariantCulture, out var seconds)
            && now - DateTimeOffset.FromUnixTimeSeconds(seconds) < CheckEvery)
        {
            return;
        }

        var id = context.Principal is null ? string.Empty : UserId(context.Principal);
        var stamp = context.Principal?.FindFirstValue(StampClaim) ?? string.Empty;
        var client = context.HttpContext.RequestServices.GetRequiredService<ObolClient>();
        try
        {
            var user = await client.UserGetAsync(id);
            if (user.SecurityStamp != stamp)
            {
                await RejectAsync(context);
                return;
            }

            context.ReplacePrincipal(Principal(user));
        }
        catch (ObolApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await RejectAsync(context);
            return;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ObolApiException)
        {
            return;   // the WebApi is down: keep the session, check again on the next request
        }

        context.Properties.Items[CheckedAtKey] = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        context.ShouldRenew = true;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
