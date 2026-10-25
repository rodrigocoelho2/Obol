using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Obol.WebApi.Extensions.Authentication;
using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Extensions.Globalization;
using Obol.WebApi.Extensions.Mvc;
using Obol.WebApi.Extensions.Security;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Connections.Services;
using Obol.WebApi.Features.Demo.Services;
using Obol.WebApi.Features.Instruments.Repositories;
using Obol.WebApi.Features.Logos.Repositories;
using Obol.WebApi.Features.Logos.Services;
using Obol.WebApi.Features.Portfolio.Repositories;
using Obol.WebApi.Features.Portfolio.Services;
using Obol.WebApi.Features.Prices.Repositories;
using Obol.WebApi.Features.Prices.Services;
using Obol.WebApi.Features.Syncs;
using Obol.WebApi.Features.Trading212;
using Obol.WebApi.Features.Users.Repositories;
using Obol.WebApi.Features.Users.Services;

namespace Obol.WebApi;

/// <summary>
/// Service registration and request pipeline of the WebApi.
/// </summary>
internal static class ObolStartup
{
    /// <summary>
    /// Registers every service. All services and repositories are singletons; each repository is registered before its
    /// service. In demo mode the Memory* repositories replace the SQL Server ones.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddObol(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var section = configuration.GetSection(ObolOptions.SectionName);
        var demoMode = section.GetValue<bool>(nameof(ObolOptions.DemoMode));
        services.AddOptions<ObolOptions>()
            .Bind(section)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ApiKey), "Obol:ApiKey is required (env Obol__ApiKey).")
            .Validate(static options => options.DemoMode || options.EncryptionKey.Length >= 16, "Obol:EncryptionKey must have at least 16 characters (env Obol__EncryptionKey, OBOL_ENCRYPTION_KEY in .env).")
            .Validate(static options => (options.Currency ?? string.Empty).Trim().Length == 3, "Obol:Currency must be a three-letter code (EUR, GBP, USD…).")
            .ValidateOnStart();

        services.AddControllers()
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new InternalControllerFeatureProvider()))
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();

        if (environment.IsDevelopment())
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options => options.EnableAnnotations());
        }

        // Outgoing calls: Trading 212, Yahoo Finance and logo.dev.
        services.AddHttpClient(Trading212Client.HttpClientName, static client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Obol/1.0 (+https://github.com/rodrigocoelho2/Obol)");
        });
        services.AddHttpClient(YahooClient.HttpClientName, static client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36");
        }).ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All, UseCookies = true });
        services.AddHttpClient(LogoService.HttpClientName, static client => client.Timeout = TimeSpan.FromSeconds(15));

        // Infrastructure
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppClock, AppClock>();
        services.AddSingleton<IGlobalizationHelper, GlobalizationHelper>();
        services.AddSingleton<ISecretProtector, SecretProtector>();

        // Repositories: SQL Server, or memory in demo mode.
        if (demoMode)
        {
            services.AddSingleton<MemoryStore>();
            services.AddSingleton<IUserRepository, MemoryUserRepository>();
            services.AddSingleton<IConnectionRepository, MemoryConnectionRepository>();
            services.AddSingleton<IInstrumentRepository, MemoryInstrumentRepository>();
            services.AddSingleton<IPortfolioRepository, MemoryPortfolioRepository>();
            services.AddSingleton<IPriceRepository, MemoryPriceRepository>();
            services.AddSingleton<ILogoRepository, MemoryLogoRepository>();
        }
        else
        {
            services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IConnectionRepository, ConnectionRepository>();
            services.AddSingleton<IInstrumentRepository, InstrumentRepository>();
            services.AddSingleton<IPortfolioRepository, PortfolioRepository>();
            services.AddSingleton<IPriceRepository, PriceRepository>();
            services.AddSingleton<ILogoRepository, LogoRepository>();
        }

        // Portfolio
        services.AddSingleton<PortfolioWriter>();
        services.AddSingleton<PortfolioLoader>();
        services.AddSingleton<IPortfolioService, PortfolioService>();

        // Prices and logos
        services.AddSingleton<IYahooClient, YahooClient>();
        services.AddSingleton<PriceQueue>();
        services.AddSingleton<PriceService>();
        services.AddSingleton<LogoService>();

        // Trading 212, the sync and CSV imports
        services.AddSingleton<ITrading212Client, Trading212Client>();
        services.AddSingleton<SyncQueue>();
        if (demoMode)
        {
            services.AddSingleton<ISyncRunner, DemoSyncRunner>();
        }
        else
        {
            services.AddSingleton<ISyncRunner, Trading212Sync>();
        }

        services.AddSingleton<IConnectionService, ConnectionService>();

        // Demo
        if (demoMode)
        {
            services.AddSingleton<IDemoSeeder, DemoSeeder>();
            services.AddHostedService<DemoStartup>();
        }
        else
        {
            services.AddSingleton<IDemoSeeder, NoDemoSeeder>();
        }

        // Users
        services.AddSingleton<IUserService, UserService>();

        // Background work
        services.AddHostedService<SyncWorker>();
        services.AddHostedService<PriceWorker>();

        return services;
    }

    /// <summary>
    /// Configures the request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseObol(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
