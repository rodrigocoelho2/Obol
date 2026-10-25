using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Obol.Library.Constants;
using Obol.WebApi.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Obol.WebApi.Extensions.Authentication;

/// <summary>
/// Authenticates the calling application by the x-api-key header (the Website is the only caller).
/// </summary>
/// <param name="options">Scheme options.</param>
/// <param name="logger">Logger factory.</param>
/// <param name="encoder">URL encoder.</param>
/// <param name="moduleOptions">Module options holding the expected key.</param>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ObolOptions> moduleOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The authentication scheme name.</summary>
    public const string SchemeName = "ApiKey";

    /// <summary>
    /// Validates the x-api-key header against the configured key (constant-time comparison).
    /// </summary>
    /// <returns>The authentication result.</returns>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expectedKey = moduleOptions.Value.ApiKey;
        if (string.IsNullOrEmpty(expectedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("No API key configured."));
        }

        if (!Request.Headers.TryGetValue(ApiHeaders.ApiKey, out var providedKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedKey.ToString()),
            Encoding.UTF8.GetBytes(expectedKey));

        if (!isValid)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "website")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
