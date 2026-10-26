using System.Text.Json;
using Microsoft.Extensions.Options;
using Obol.Library.Enums;
using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Extensions.Security;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features.Connections.Models.Entities;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Instruments.Repositories;
using Obol.WebApi.Features.Instruments.Services;
using Obol.WebApi.Features.Portfolio.Models.Entities;
using Obol.WebApi.Features.Portfolio.Repositories;
using Obol.WebApi.Features.Portfolio.Services;
using Obol.WebApi.Features.Prices.Repositories;
using Obol.WebApi.Features.Trading212;
using Obol.WebApi.Features.Users.Repositories;
using Obol.WebApi.Features.Users.Services;

namespace Obol.WebApi.Features.Demo.Services;

/// <summary>
/// Fills an account with the sample portfolio in demo mode; does nothing otherwise.
/// </summary>
internal interface IDemoSeeder
{
    /// <summary>
    /// Fills an account with <see cref="DemoData"/>.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task SeedAsync(Guid userId);
}

/// <summary>
/// Outside demo mode: new accounts start empty and wait for Trading 212 or a CSV export.
/// </summary>
internal sealed class NoDemoSeeder : IDemoSeeder
{
    /// <inheritdoc />
    public Task SeedAsync(Guid userId) => Task.CompletedTask;
}

/// <summary>
/// Demo mode: saves <see cref="DemoData"/> through the same writer an import uses, with positions, a snapshot and a
/// Trading 212 connection that looks like it synced a minute ago.
/// </summary>
/// <param name="writer">Saves the trades, dividends and cash.</param>
/// <param name="instrumentRepository">The benchmarks and the sectors.</param>
/// <param name="portfolioRepository">Positions and snapshots.</param>
/// <param name="priceRepository">Prices.</param>
/// <param name="connectionRepository">The connection and the sync log.</param>
/// <param name="protector">Encrypts the (fake) key.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class DemoSeeder(
    PortfolioWriter writer,
    IInstrumentRepository instrumentRepository,
    IPortfolioRepository portfolioRepository,
    IPriceRepository priceRepository,
    IConnectionRepository connectionRepository,
    ISecretProtector protector,
    IAppClock clock) : IDemoSeeder
{
    /// <inheritdoc />
    public async Task SeedAsync(Guid userId)
    {
        var today = clock.Today;
        var demo = DemoData.Generate(today);
        var counts = await writer.SaveAsync(userId, demo.Batch);

        var prices = new List<PriceInput>();
        foreach (var (isin, series) in demo.Prices)
        {
            if (counts.Ids.TryGetValue(isin, out var id))
            {
                prices.AddRange(series.Select(pair => new PriceInput(id, DbTime.Day(pair.Key), pair.Value, (byte)DataSource.Demo)));
            }
        }

        foreach (var row in await instrumentRepository.UpsertAsync(Benchmarks.Inputs))
        {
            if (demo.Benchmarks.TryGetValue(row.InstrumentKey, out var closes))
            {
                prices.AddRange(closes.Select(pair => new PriceInput(row.InstrumentID, DbTime.Day(pair.Key), pair.Value, (byte)DataSource.Demo)));
            }
        }

        foreach (var (isin, profile) in demo.Sectors)
        {
            if (counts.Ids.TryGetValue(isin, out var id))
            {
                await instrumentRepository.SetProfileAsync(id, profile.Sector, profile.Industry);
            }
        }

        await priceRepository.SavePricesAsync(prices);
        await priceRepository.SaveFxAsync(demo.UsdEur.Select(pair => new FxInput("USD", DemoData.Currency, DbTime.Day(pair.Key), pair.Value)).ToList());

        // Positions as Trading 212 would report them: the lots of the trades at today's prices.
        var trades = await portfolioRepository.ListTradesAsync(userId);
        var holdings = new List<HoldingInput>();
        foreach (var (instrumentId, lot) in PortfolioMath.Lots(trades))
        {
            if (lot.Quantity <= 0)
            {
                continue;
            }

            var isin = counts.Ids.First(pair => pair.Value == instrumentId).Key;
            var series = demo.Prices[isin];
            var price = series.Values[^1];
            var fx = demo.Batch.Instruments[isin].Currency == "USD" ? demo.UsdEur.Values[^1] : 1m;
            var value = Math.Round(lot.Quantity * price * fx, 2);
            var cost = Math.Round(lot.Cost, 2);
            holdings.Add(new HoldingInput(instrumentId, lot.Quantity, lot.AveragePrice, price, value, cost, value - cost, null,
                lot.OpenedAt is { } opened ? DbTime.Time(opened) : null));
        }

        await portfolioRepository.ReplaceHoldingsAsync(userId, holdings);

        // Pies, as the Trading 212 pies endpoints would give them.
        var pies = new List<PieInput>();
        var pieHoldings = new List<PieHoldingInput>();
        foreach (var (pieId, name, icon, goal, parts) in DemoData.Pies)
        {
            var owned = new List<PieHoldingInput>();
            foreach (var (isin, share) in parts)
            {
                if (counts.Ids.TryGetValue(isin, out var instrumentId) && holdings.FirstOrDefault(holding => holding.InstrumentID == instrumentId) is { } holding)
                {
                    var value = Math.Round(holding.Value * share, 2);
                    var cost = Math.Round(holding.Cost * share, 2);
                    owned.Add(new PieHoldingInput(pieId, instrumentId, holding.Quantity * share, cost, value, value - cost, null, null));
                }
            }

            var pieValue = owned.Sum(static holding => holding.Value);
            owned = owned.Select(holding => holding with
            {
                CurrentShare = pieValue > 0 ? Math.Round(holding.Value / pieValue, 4) : null,
                ExpectedShare = Math.Round(1m / owned.Count, 4),
            }).ToList();
            var invested = owned.Sum(static holding => holding.Invested);
            pies.Add(new PieInput(pieId, name, icon, 0.37m, invested, pieValue, pieValue - invested, 0m, goal, DbTime.Time(today.AddYears(-DemoData.Years).ToDateTime(new TimeOnly(9, 0)))));
            pieHoldings.AddRange(owned);
        }

        await portfolioRepository.ReplacePiesAsync(userId, pies, pieHoldings);
        await portfolioRepository.SaveSnapshotAsync(userId, today, new SnapshotRow
        {
            TotalValue = holdings.Sum(static holding => holding.Value) + demo.Cash,
            Cash = demo.Cash,
            Cost = holdings.Sum(static holding => holding.Cost),
            UnrealizedPL = holdings.Sum(static holding => holding.UnrealizedPL),
            RealizedPL = demo.Batch.Trades.Sum(static trade => trade.RealizedPL ?? 0m),
        });

        await connectionRepository.SaveAsync(new ConnectionRow
        {
            UserUniqueID = userId,
            Environment = "demo",
            ApiKeyCipher = protector.Protect("demo-key-7f3a"),
            ApiSecretCipher = protector.Protect("demo-secret"),
            KeyHint = "••7f3a",
            AccountId = 31415926,
            Currency = DemoData.Currency,
        });
        await DemoSyncRunner.MarkSyncedAsync(connectionRepository, clock, userId, holdings.Count, demo.Batch);
    }
}

/// <summary>
/// "Sync now" in demo mode: nothing to fetch, so the connection is marked synced and the run logged.
/// </summary>
/// <param name="connectionRepository">The connection and the sync log.</param>
/// <param name="portfolioRepository">The portfolio.</param>
/// <param name="clock">The app's clock.</param>
internal sealed class DemoSyncRunner(IConnectionRepository connectionRepository, IPortfolioRepository portfolioRepository, IAppClock clock) : ISyncRunner
{
    /// <inheritdoc />
    public async Task<bool> RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await connectionRepository.GetAsync(userId) is null)
        {
            return false;
        }

        await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);
        var positions = (await portfolioRepository.ListHoldingsAsync(userId)).Count;
        await MarkSyncedAsync(connectionRepository, clock, userId, positions, null);
        return true;
    }

    /// <summary>
    /// Marks the demo connection synced and logs a run.
    /// </summary>
    /// <param name="connectionRepository">The connection and the sync log.</param>
    /// <param name="clock">The app's clock.</param>
    /// <param name="userId">The account.</param>
    /// <param name="positions">How many positions.</param>
    /// <param name="first">The demo data on the first run (its history), or null.</param>
    /// <returns>A task.</returns>
    internal static async Task MarkSyncedAsync(IConnectionRepository connectionRepository, IAppClock clock, Guid userId, int positions, ImportBatch? first)
    {
        var now = clock.UtcNow;
        var summary = first is null
            ? $"{positions} positions · no new trades · no new dividends · no new transfers"
            : $"{positions} positions · {first.Trades.Count} new trades · {first.Dividends.Count} new dividends · {first.Cash.Count} new transfers";
        var lines = new List<string>
        {
            $"Account 31415926 · EUR (demo data)",
            $"{positions} positions",
            first is null ? "Orders: 1 page, no new trades" : $"Orders: {Pages(first.Trades.Count)}, {first.Trades.Count} new trades",
            first is null ? "Dividends: 1 page, no new dividends" : $"Dividends: {Pages(first.Dividends.Count)}, {first.Dividends.Count} new dividends",
            first is null ? "Deposits, withdrawals and interest: 1 page, no new transfers" : $"Deposits, withdrawals and interest: {Pages(first.Cash.Count)}, {first.Cash.Count} new transfers",
        };
        await connectionRepository.MarkAsync(userId, (byte)SyncState.Ok, summary, now, now, 31415926, DemoData.Currency);
        await connectionRepository.SaveRunAsync(userId, new SyncRunRow
        {
            Source = (byte)DataSource.Trading212,
            StartedAt = now.AddSeconds(first is null ? -2 : -96),
            FinishedAt = now,
            Ok = true,
            Summary = summary,
            Lines = JsonSerializer.Serialize(lines),
        });
    }

    private static string Pages(int items)
    {
        var pages = (items / 50) + 1;
        return pages == 1 ? "1 page" : $"{pages} pages";
    }
}

/// <summary>
/// Demo mode: creates the "demo" account (password <see cref="ObolOptions.DemoPassword"/>) with the sample portfolio
/// when the WebApi starts.
/// </summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="demoSeeder">Fills the account.</param>
/// <param name="options">Module options.</param>
/// <param name="logger">Logger.</param>
internal sealed class DemoStartup(
    IUserRepository userRepository,
    IDemoSeeder demoSeeder,
    IOptions<ObolOptions> options,
    ILogger<DemoStartup> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await userRepository.GetByNameAsync(UserService.DemoUserName) is not null)
        {
            return;
        }

        var user = await userRepository.CreateAsync(UserService.DemoUserName, PasswordHasher.Hash(options.Value.DemoPassword), Guid.NewGuid().ToString("N"));
        if (user is not null)
        {
            await demoSeeder.SeedAsync(user.UserUniqueID);
            logger.LogInformation("Demo mode: sign in as {UserName} / {Password}", UserService.DemoUserName, options.Value.DemoPassword);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
