using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Features.Connections.Models.Entities;

namespace Obol.WebApi.Features.Connections.Repositories;

/// <summary>
/// The Trading 212 connections and the sync log.
/// </summary>
internal interface IConnectionRepository
{
    /// <summary>Gets the connection of an account.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection, or null.</returns>
    Task<ConnectionRow?> GetAsync(Guid userId);

    /// <summary>Lists every connection.</summary>
    /// <returns>The connections.</returns>
    Task<List<ConnectionRow>> ListAsync();

    /// <summary>Saves (or replaces) the key and secret of an account; the state starts over.</summary>
    /// <param name="row">The connection.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(ConnectionRow row);

    /// <summary>Saves what the sync reads.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="positions">Positions and cash.</param>
    /// <param name="orders">Orders.</param>
    /// <param name="dividends">Dividends.</param>
    /// <param name="transactions">Deposits, withdrawals and interest.</param>
    /// <returns>A task.</returns>
    Task SetOptionsAsync(Guid userId, bool positions, bool orders, bool dividends, bool transactions);

    /// <summary>Records how a sync went (null leaves a value as it is).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="state">The state.</param>
    /// <param name="message">The message.</param>
    /// <param name="lastSyncAt">When it finished.</param>
    /// <param name="historyDoneAt">When the whole history was read.</param>
    /// <param name="accountId">The Trading 212 account id.</param>
    /// <param name="currency">The account currency.</param>
    /// <returns>A task.</returns>
    Task MarkAsync(Guid userId, byte state, string message, DateTime? lastSyncAt = null, DateTime? historyDoneAt = null, long? accountId = null, string? currency = null);

    /// <summary>Forgets the key and secret.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid userId);

    /// <summary>Logs a sync or an import (keeps the last 30).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="run">The run.</param>
    /// <returns>A task.</returns>
    Task SaveRunAsync(Guid userId, SyncRunRow run);

    /// <summary>Lists the last runs, newest first.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="top">How many.</param>
    /// <returns>The runs.</returns>
    Task<List<SyncRunRow>> ListRunsAsync(Guid userId, int top);
}

/// <summary>Connections in SQL Server.</summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class ConnectionRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IConnectionRepository
{
    /// <inheritdoc />
    public Task<ConnectionRow?> GetAsync(Guid userId) => QuerySingleOrDefaultAsync<ConnectionRow>("[dbo].[BrokerConnectionGet]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task<List<ConnectionRow>> ListAsync() => QueryAsync<ConnectionRow>("[dbo].[BrokerConnectionList]");

    /// <inheritdoc />
    public Task SaveAsync(ConnectionRow row) => ExecuteAsync("[dbo].[BrokerConnectionSave]", new
    {
        row.UserUniqueID,
        row.Environment,
        row.ApiKeyCipher,
        row.ApiSecretCipher,
        row.KeyHint,
        row.AccountId,
        row.Currency,
    });

    /// <inheritdoc />
    public Task SetOptionsAsync(Guid userId, bool positions, bool orders, bool dividends, bool transactions) =>
        ExecuteAsync("[dbo].[BrokerConnectionSetOptions]", new
        {
            UserUniqueID = userId,
            SyncPositions = positions,
            SyncOrders = orders,
            SyncDividends = dividends,
            SyncTransactions = transactions,
        });

    /// <inheritdoc />
    public Task MarkAsync(Guid userId, byte state, string message, DateTime? lastSyncAt = null, DateTime? historyDoneAt = null, long? accountId = null, string? currency = null) =>
        ExecuteAsync("[dbo].[BrokerConnectionMark]", new
        {
            UserUniqueID = userId,
            State = state,
            LastMessage = message,
            LastSyncAt = lastSyncAt,
            HistoryDoneAt = historyDoneAt,
            AccountId = accountId,
            Currency = currency,
        });

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId) => ExecuteAsync("[dbo].[BrokerConnectionDelete]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task SaveRunAsync(Guid userId, SyncRunRow run) => ExecuteAsync("[dbo].[SyncRunSave]", new
    {
        UserUniqueID = userId,
        run.Source,
        run.StartedAt,
        run.FinishedAt,
        run.Ok,
        run.Summary,
        run.Lines,
    });

    /// <inheritdoc />
    public Task<List<SyncRunRow>> ListRunsAsync(Guid userId, int top) => QueryAsync<SyncRunRow>("[dbo].[SyncRunList]", new { UserUniqueID = userId, Top = top });
}

/// <summary>Connections in demo mode.</summary>
/// <param name="store">The in-memory tables.</param>
/// <param name="timeProvider">The clock.</param>
internal sealed class MemoryConnectionRepository(MemoryStore store, TimeProvider timeProvider) : IConnectionRepository
{
    private long _nextRunId;

    /// <inheritdoc />
    public Task<ConnectionRow?> GetAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Connections.TryGetValue(userId, out var row) ? Copy(row) : null);
        }
    }

    /// <inheritdoc />
    public Task<List<ConnectionRow>> ListAsync()
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Connections.Values.Select(Copy).ToList());
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(ConnectionRow row)
    {
        lock (store.Gate)
        {
            if (store.Users.All(user => user.UserUniqueID != row.UserUniqueID))
            {
                return Task.CompletedTask;
            }

            var saved = Copy(row);
            if (store.Connections.TryGetValue(row.UserUniqueID, out var old))
            {
                saved.SyncPositions = old.SyncPositions;
                saved.SyncOrders = old.SyncOrders;
                saved.SyncDividends = old.SyncDividends;
                saved.SyncTransactions = old.SyncTransactions;
                saved.LastSyncAt = old.LastSyncAt;
                saved.CreatedAt = old.CreatedAt;
            }
            else
            {
                saved.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
            }

            saved.State = 0;
            saved.LastMessage = string.Empty;
            saved.HistoryDoneAt = null;
            store.Connections[row.UserUniqueID] = saved;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetOptionsAsync(Guid userId, bool positions, bool orders, bool dividends, bool transactions)
    {
        lock (store.Gate)
        {
            if (store.Connections.TryGetValue(userId, out var row))
            {
                row.SyncPositions = positions;
                row.SyncOrders = orders;
                row.SyncDividends = dividends;
                row.SyncTransactions = transactions;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkAsync(Guid userId, byte state, string message, DateTime? lastSyncAt = null, DateTime? historyDoneAt = null, long? accountId = null, string? currency = null)
    {
        lock (store.Gate)
        {
            if (store.Connections.TryGetValue(userId, out var row))
            {
                row.State = state;
                row.LastMessage = message.Length > 400 ? message[..400] : message;
                row.LastSyncAt = lastSyncAt ?? row.LastSyncAt;
                row.HistoryDoneAt = historyDoneAt ?? row.HistoryDoneAt;
                row.AccountId = accountId ?? row.AccountId;
                row.Currency = currency ?? row.Currency;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId)
    {
        lock (store.Gate)
        {
            store.Connections.Remove(userId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveRunAsync(Guid userId, SyncRunRow run)
    {
        lock (store.Gate)
        {
            var runs = MemoryStore.For(store.Runs, userId);
            runs.Insert(0, new SyncRunRow
            {
                SyncRunID = Interlocked.Increment(ref _nextRunId),
                Source = run.Source,
                StartedAt = run.StartedAt,
                FinishedAt = run.FinishedAt,
                Ok = run.Ok,
                Summary = run.Summary,
                Lines = run.Lines,
            });
            if (runs.Count > 30)
            {
                runs.RemoveRange(30, runs.Count - 30);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<SyncRunRow>> ListRunsAsync(Guid userId, int top)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Runs.TryGetValue(userId, out var runs) ? runs.Take(top).ToList() : []);
        }
    }

    private static ConnectionRow Copy(ConnectionRow row) => new()
    {
        UserUniqueID = row.UserUniqueID,
        Environment = row.Environment,
        ApiKeyCipher = row.ApiKeyCipher,
        ApiSecretCipher = row.ApiSecretCipher,
        KeyHint = row.KeyHint,
        AccountId = row.AccountId,
        Currency = row.Currency,
        SyncPositions = row.SyncPositions,
        SyncOrders = row.SyncOrders,
        SyncDividends = row.SyncDividends,
        SyncTransactions = row.SyncTransactions,
        State = row.State,
        LastMessage = row.LastMessage,
        LastSyncAt = row.LastSyncAt,
        HistoryDoneAt = row.HistoryDoneAt,
        CreatedAt = row.CreatedAt,
    };
}
