using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Obol.Sdk;
using Obol.Website.Security;

namespace Obol.Website;

/// <summary>
/// Service registration and request pipeline of the Website.
/// </summary>
internal static class ObolWebsiteStartup
{
    /// <summary>
    /// Registers MVC, anti-forgery, the sign-in (cookie) and the WebApi client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddObolWebsite(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebsiteOptions>(configuration.GetSection(WebsiteOptions.SectionName));
        Helpers.Clock.Use(configuration.GetSection(WebsiteOptions.SectionName).Get<WebsiteOptions>()?.TimeZone ?? "Europe/Lisbon");
        services.AddObolAuthentication(configuration);

        services.AddControllersWithViews()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // The page scripts send the token in this header on every POST.
        services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

        services.AddObolClient(configuration);

        // Behind a reverse proxy (Nginx Proxy Manager, Cloudflare) the request arrives as plain HTTP: take the real scheme
        // and client from X-Forwarded-Proto / X-Forwarded-For, so links and the cookie are HTTPS.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Configures the request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseObolWebsite(this WebApplication app)
    {
        app.UseForwardedHeaders();

        var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        // Static files are public (the landing page, the sign-in form and the app icons need them).
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            // CSS and JS are linked with ?v=<hash of the file> (asp-append-version): those, the fonts and the icons are kept
            // by the browser. Anything else (the service worker, the manifest) is checked on every load.
            OnPrepareResponse = context =>
            {
                var request = context.Context.Request;
                var path = request.Path.Value ?? string.Empty;
                context.Context.Response.Headers.CacheControl = request.Query.ContainsKey("v")
                    ? "public, max-age=31536000, immutable"
                    : path.StartsWith("/fonts/", StringComparison.Ordinal) || path.StartsWith("/img/", StringComparison.Ordinal)
                        ? "public, max-age=2592000"
                        : "no-cache";
            },
        });

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}");

        return app;
    }
}
