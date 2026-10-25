using Obol.Library.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Obol.Sdk;

/// <summary>
/// Where the WebApi is and the key to call it, bound from the "ObolApi" section.
/// </summary>
public sealed class ObolClientOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ObolApi";

    /// <summary>Gets or sets the WebApi base URL, e.g. http://webapi:8080/.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the key sent in x-api-key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the request timeout (a big CSV import can take a while).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}

/// <summary>
/// Registers <see cref="ObolClient"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the typed client with its base address and x-api-key header.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding the "ObolApi" section.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddObolClient(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ObolClientOptions.SectionName).Get<ObolClientOptions>() ?? new ObolClientOptions();
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException("ObolApi:Url must be an absolute URL (env ObolApi__Url).");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("ObolApi:ApiKey is required (env ObolApi__ApiKey).");
        }

        // A trailing slash makes relative paths ("portfolio/holdings") append to the base path instead of replacing it.
        var normalizedBase = baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri(baseAddress.AbsoluteUri + "/");

        services.AddHttpClient<ObolClient>(client =>
        {
            client.BaseAddress = normalizedBase;
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add(ApiHeaders.ApiKey, options.ApiKey);
        });

        return services;
    }
}
