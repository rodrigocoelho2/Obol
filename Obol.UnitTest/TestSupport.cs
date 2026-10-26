using Obol.Library.Enums;
using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Extensions.Globalization;
using Obol.WebApi.Extensions.Security;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Demo.Services;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Instruments.Repositories;
using Obol.WebApi.Features.Portfolio.Models.Entities;
using Obol.WebApi.Features.Portfolio.Repositories;
using Obol.WebApi.Features.Portfolio.Services;
using Obol.WebApi.Features.Prices.Repositories;
using Obol.WebApi.Features.Users.Models.DTOs;
using Obol.WebApi.Features.Users.Repositories;
using Obol.WebApi.Features.Users.Services;
using Microsoft.Extensions.Options;

namespace Obol.UnitTest;

/// <summary>Shared helpers: a fixed clock, options and hand-made portfolio data.</summary>
internal static class TestSupport
{
    /// <summary>Friday 23 October 2026, 18:40 in Lisbon (17:40 UTC).</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 17, 40, 0, TimeSpan.Zero);

    public static DateOnly Today => new(2026, 10, 23);

    public static IOptions<ObolOptions> Options(bool demo = false, Action<ObolOptions>? configure = null)
    {
        var options = new ObolOptions
        {
            ApiKey = "test-key",
            EncryptionKey = "test-encryption-key-0123456789",
            TimeZone = "Europe/Lisbon",
            Currency = "EUR",
            DemoMode = demo,
        };
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    /// <summary>Midday UTC of a day (the same local day in Lisbon all year).</summary>
    public static DateTime At(DateOnly day, int hour = 12) => day.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);

    public static InstrumentRow Instrument(int id, string symbol, string currency = "EUR", AssetClass assetClass = AssetClass.EquityEtf) => new()
    {
        InstrumentID = id,
        InstrumentKey = "T212:" + symbol,
        Ticker = symbol,
        Symbol = symbol,
        Name = symbol + " name",
        Currency = currency,
        AssetClass = (byte)assetClass,
    };

    public static TradeRow Buy(int instrumentId, DateOnly day, decimal quantity, decimal price, decimal? total = null, string currency = "EUR") => new()
    {
        InstrumentID = instrumentId,
        Side = 1,
        Quantity = quantity,
        Price = price,
        PriceCurrency = currency,
        Total = total ?? quantity * price,
        ExecutedAt = At(day),
    };

    public static TradeRow Sell(int instrumentId, DateOnly day, decimal quantity, decimal price, decimal? total = null, string currency = "EUR") => new()
    {
        InstrumentID = instrumentId,
        Side = 2,
        Quantity = quantity,
        Price = price,
        PriceCurrency = currency,
        Total = total ?? quantity * price,
        ExecutedAt = At(day),
    };

    public static CashMovementRow Cash(CashKind kind, DateOnly day, decimal amount) => new() { Kind = (byte)kind, Amount = amount, At = At(day, 9) };

    public static DividendRow Dividend(int instrumentId, DateOnly day, decimal amount, decimal? quantity = null) => new()
    {
        InstrumentID = instrumentId,
        PaidAt = At(day, 10),
        Amount = amount,
        Quantity = quantity,
        Kind = "DIVIDEND",
    };

    /// <summary>Portfolio data worked out from the trades (no Trading 212 positions), in euros.</summary>
    public static PortfolioData Data(
        IEnumerable<InstrumentRow> instruments,
        IEnumerable<TradeRow>? trades = null,
        IEnumerable<CashMovementRow>? cash = null,
        IEnumerable<DividendRow>? dividends = null,
        PriceBook? prices = null,
        DateOnly? today = null) => new()
        {
            Currency = "EUR",
            Today = today ?? Today,
            Instruments = instruments.ToDictionary(static instrument => instrument.InstrumentID),
            Holdings = [],
            BrokerHoldings = false,
            Trades = (trades ?? []).OrderBy(static trade => trade.ExecutedAt).ToList(),
            Dividends = (dividends ?? []).OrderBy(static dividend => dividend.PaidAt).ToList(),
            Cash = (cash ?? []).OrderBy(static movement => movement.At).ToList(),
            Snapshots = [],
            Prices = prices ?? new PriceBook("EUR"),
            LocalDay = static utc => DateOnly.FromDateTime(utc),
        };
}

/// <summary>A clock stopped at <see cref="TestSupport.Now"/>.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Value { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Value;
}

/// <summary>
/// The services of Obol.WebApi on one in-memory store, as demo mode wires them.
/// </summary>
internal sealed class TestApp
{
    public TestApp(bool demo = false)
    {
        Time = new FixedTimeProvider(TestSupport.Now);
        Options = TestSupport.Options(demo);
        Clock = new AppClock(Time, Options);
        var globalization = new GlobalizationHelper();
        Store = new MemoryStore();
        Users = new MemoryUserRepository(Store, Time);
        Instruments = new MemoryInstrumentRepository(Store);
        Portfolio = new MemoryPortfolioRepository(Store, Time);
        Prices = new MemoryPriceRepository(Store);
        Connections = new MemoryConnectionRepository(Store, Time);
        Protector = new SecretProtector(Options);
        Writer = new PortfolioWriter(Instruments, Portfolio);
        Seeder = demo ? new DemoSeeder(Writer, Instruments, Portfolio, Prices, Connections, Protector, Clock) : new NoDemoSeeder();
        UserService = new UserService(Users, Seeder, globalization, Options);
        Loader = new PortfolioLoader(Portfolio, Instruments, Prices, Connections, Clock, Options);
        PortfolioService = new PortfolioService(Loader, Connections, globalization);
    }

    public FixedTimeProvider Time { get; }

    public IOptions<ObolOptions> Options { get; }

    public IAppClock Clock { get; }

    public MemoryStore Store { get; }

    public MemoryUserRepository Users { get; }

    public MemoryInstrumentRepository Instruments { get; }

    public MemoryPortfolioRepository Portfolio { get; }

    public MemoryPriceRepository Prices { get; }

    public MemoryConnectionRepository Connections { get; }

    public SecretProtector Protector { get; }

    public PortfolioWriter Writer { get; }

    public IDemoSeeder Seeder { get; }

    public UserService UserService { get; }

    public PortfolioLoader Loader { get; }

    public PortfolioService PortfolioService { get; }

    /// <summary>Creates an account (seeded with the demo portfolio when the app runs in demo mode) and returns its id.</summary>
    public async Task<string> CreateUserAsync(string name = "rodas")
    {
        var created = await UserService.CreateAsync(new UserCreateRequest { UserName = name, Password = "obol-2026-test" });
        Assert.True(created.IsSuccess, created.Problems?.Title);
        return created.Result!.Id;
    }
}
