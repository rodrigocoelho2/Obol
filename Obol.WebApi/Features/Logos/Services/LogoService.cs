using Microsoft.Extensions.Options;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features.Instruments.Services;
using Obol.WebApi.Features.Logos.Repositories;

namespace Obol.WebApi.Features.Logos.Services;

/// <summary>A logo to serve.</summary>
/// <param name="ContentType">The content type.</param>
/// <param name="Data">The image.</param>
internal sealed record Logo(string ContentType, byte[] Data);

/// <summary>
/// Company and fund logos: fetched once from logo.dev (by ISIN, or by symbol), kept in the database and served from
/// there. Without <see cref="ObolOptions.LogoToken"/> there are no logos and the screens show ticker badges.
/// </summary>
/// <param name="logoRepository">The logos kept.</param>
/// <param name="httpClientFactory">Makes the HttpClient.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="options">Module options (the logo.dev key).</param>
/// <param name="logger">Logger.</param>
internal sealed class LogoService(
    ILogoRepository logoRepository,
    IHttpClientFactory httpClientFactory,
    IAppClock clock,
    IOptions<ObolOptions> options,
    ILogger<LogoService> logger)
{
    /// <summary>The name of the HttpClient.</summary>
    public const string HttpClientName = "logos";

    private const int MaxBytes = 512 * 1024;
    private static readonly TimeSpan RetryMissingAfter = TimeSpan.FromDays(7);

    /// <summary>Gets a value indicating whether logos are on (a logo.dev key is set).</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(options.Value.LogoToken);

    /// <summary>
    /// The logo of an instrument.
    /// </summary>
    /// <param name="key">The instrument key (ISIN or T212:ticker).</param>
    /// <param name="symbol">The symbol, for instruments without an ISIN.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>The logo, or null when there is none.</returns>
    public async Task<Logo?> GetAsync(string key, string? symbol, CancellationToken cancellationToken)
    {
        key = (key ?? string.Empty).Trim();
        if (key.Length is 0 or > 40)
        {
            return null;
        }

        var kept = await logoRepository.GetAsync(key);
        if (kept is { Data: { Length: > 0 } data })
        {
            return new Logo(kept.ContentType.Length > 0 ? kept.ContentType : "image/png", data);
        }

        if (!Enabled || (kept is not null && clock.UtcNow - kept.FetchedAt < RetryMissingAfter))
        {
            return null;
        }

        var isin = InstrumentNames.CleanIsin(key);
        var bare = (symbol ?? (key.StartsWith("T212:", StringComparison.Ordinal) ? InstrumentNames.Symbol(key[5..]) : string.Empty)).Trim();
        if (isin is null && bare.Length == 0)
        {
            return null;
        }

        var path = isin is not null ? $"isin/{isin}" : $"ticker/{Uri.EscapeDataString(bare)}";
        var url = $"https://img.logo.dev/{path}?token={Uri.EscapeDataString(options.Value.LogoToken.Trim())}&size=128&format=png&fallback=404";
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await logoRepository.SaveAsync(key, string.Empty, null);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var type = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            if (bytes.Length == 0 || bytes.Length > MaxBytes || !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                await logoRepository.SaveAsync(key, string.Empty, null);
                return null;
            }

            await logoRepository.SaveAsync(key, type, bytes);
            return new Logo(type, bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Logo of {Key} not fetched ({Message})", key, exception.Message);

            return null;
        }
    }
}
