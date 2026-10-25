using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Obol.Library.Enums;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Prices.Services;
using Obol.WebApi.Features.Trading212;
using Obol.WebApi.Features.Users.Repositories;

namespace Obol.WebApi.Features.Syncs;

/// <summary>
/// Accounts waiting for a sync. One entry per account at a time; <see cref="IsBusy"/> covers waiting and running.
/// </summary>
internal sealed class SyncQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _busy = new();

    /// <summary>Gets the reader the worker takes accounts from.</summary>
    public ChannelReader<Guid> Reader => _channel.Reader;

    /// <summary>
    /// Asks for a sync.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>False when the account is already waiting or syncing.</returns>
    public bool Enqueue(Guid userId) => _busy.TryAdd(userId, 0) && _channel.Writer.TryWrite(userId);

    /// <summary>
    /// Whether an account is waiting or syncing.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>True when busy.</returns>
    public bool IsBusy(Guid userId) => _busy.ContainsKey(userId);

    /// <summary>
    /// Marks a sync as finished.
    /// </summary>
    /// <param name="userId">The account.</param>
    public void Done(Guid userId) => _busy.TryRemove(userId, out _);
}

/// <summary>
/// Runs the syncs one after the other (the queue, from "Sync now" and new connections) and queues every connection again
/// every <see cref="ObolOptions.SyncMinutes"/>.
/// </summary>
/// <param name="queue">The queue.</param>
/// <param name="runner">Runs one sync.</param>
/// <param name="connectionRepository">The connections.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="options">Module options.</param>
/// <param name="logger">Logger.</param>
internal sealed class SyncWorker(
    SyncQueue queue,
    ISyncRunner runner,
    IConnectionRepository connectionRepository,
    IAppClock clock,
    IOptions<ObolOptions> options,
    ILogger<SyncWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = ScheduleAsync(stoppingToken);
        try
        {
            await foreach (var userId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await runner.RunAsync(userId, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Sync of {UserId} failed", userId);
                    try
                    {
                        await connectionRepository.MarkAsync(userId, (byte)SyncState.Failed, "The sync stopped with an error (see the WebApi log).");
                    }
                    catch (Exception markException)
                    {
                        logger.LogWarning(markException, "Could not record the failed sync of {UserId}", userId);
                    }
                }
                finally
                {
                    queue.Done(userId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }

        await schedule;
    }

    private async Task ScheduleAsync(CancellationToken stoppingToken)
    {
        var minutes = options.Value.SyncMinutes;
        if (minutes <= 0)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = clock.UtcNow;
                    foreach (var connection in await connectionRepository.ListAsync())
                    {
                        if (connection.LastSyncAt is null || now - connection.LastSyncAt.Value >= TimeSpan.FromMinutes(minutes) || connection.State == (byte)SyncState.Running)
                        {
                            if (!queue.IsBusy(connection.UserUniqueID))
                            {
                                queue.Enqueue(connection.UserUniqueID);
                            }
                        }
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "Could not list the connections to sync");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}

/// <summary>
/// Fetches price history for the accounts in the <see cref="PriceQueue"/> (after a sync or an import) and refreshes
/// every account's prices four times a day.
/// </summary>
/// <param name="queue">The queue.</param>
/// <param name="priceService">Fetches prices.</param>
/// <param name="userRepository">The accounts.</param>
/// <param name="options">Module options.</param>
/// <param name="logger">Logger.</param>
internal sealed class PriceWorker(
    PriceQueue queue,
    PriceService priceService,
    IUserRepository userRepository,
    IOptions<ObolOptions> options,
    ILogger<PriceWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.PriceHistory || options.Value.DemoMode)
        {
            return;
        }

        var refresh = RefreshAsync(stoppingToken);
        try
        {
            await foreach (var userId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                queue.Taken(userId);
                try
                {
                    var saved = await priceService.UpdateAccountAsync(userId, stoppingToken);
                    if (saved > 0 && logger.IsEnabled(LogLevel.Information))
                        logger.LogInformation("Saved {Count} daily prices for {UserId}", saved, userId);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "Price history of {UserId} failed", userId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }

        await refresh;
    }

    private async Task RefreshAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var user in await userRepository.ListAsync())
                    {
                        queue.Enqueue(user.UserUniqueID);
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "Could not list the accounts for the price refresh");
                }

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
