using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Features.Portfolio.Models.Entities;

namespace Obol.WebApi.Features.Portfolio.Repositories;

/// <summary>
/// The portfolio of an account: open positions, trades, dividends, cash movements and daily snapshots.
/// </summary>
internal interface IPortfolioRepository
{
    /// <summary>Replaces the open positions.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The positions (empty closes them all).</param>
    /// <returns>A task.</returns>
    Task ReplaceHoldingsAsync(Guid userId, IReadOnlyCollection<HoldingInput> rows);

    /// <summary>Lists the open positions.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The positions.</returns>
    Task<List<HoldingRow>> ListHoldingsAsync(Guid userId);

    /// <summary>Replaces the Trading 212 pies.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="pies">The pies (empty removes them all).</param>
    /// <param name="holdings">The instruments of the pies.</param>
    /// <returns>A task.</returns>
    Task ReplacePiesAsync(Guid userId, IReadOnlyCollection<PieInput> pies, IReadOnlyCollection<PieHoldingInput> holdings);

    /// <summary>Lists the Trading 212 pies.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The pies.</returns>
    Task<List<PieRow>> ListPiesAsync(Guid userId);

    /// <summary>Lists the instruments of every pie.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The instruments.</returns>
    Task<List<PieHoldingRow>> ListPieHoldingsAsync(Guid userId);

    /// <summary>Adds trades (one already kept is skipped).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The trades.</param>
    /// <returns>How many were new.</returns>
    Task<int> SaveTradesAsync(Guid userId, IReadOnlyCollection<TradeInput> rows);

    /// <summary>Lists every trade, oldest first.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The trades.</returns>
    Task<List<TradeRow>> ListTradesAsync(Guid userId);

    /// <summary>Adds dividends (one already kept is skipped).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The dividends.</param>
    /// <returns>How many were new.</returns>
    Task<int> SaveDividendsAsync(Guid userId, IReadOnlyCollection<DividendInput> rows);

    /// <summary>Lists every dividend, oldest first.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The dividends.</returns>
    Task<List<DividendRow>> ListDividendsAsync(Guid userId);

    /// <summary>Adds cash movements (one already kept is skipped).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="rows">The movements.</param>
    /// <returns>How many were new.</returns>
    Task<int> SaveCashAsync(Guid userId, IReadOnlyCollection<CashInput> rows);

    /// <summary>Lists every cash movement, oldest first.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The movements.</returns>
    Task<List<CashMovementRow>> ListCashAsync(Guid userId);

    /// <summary>Keeps today's snapshot (replacing an earlier one of the same day).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="day">The day.</param>
    /// <param name="snapshot">The values.</param>
    /// <returns>A task.</returns>
    Task SaveSnapshotAsync(Guid userId, DateOnly day, SnapshotRow snapshot);

    /// <summary>Lists the snapshots, oldest first.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The snapshots.</returns>
    Task<List<SnapshotRow>> ListSnapshotsAsync(Guid userId);

    /// <summary>Deletes everything imported for the account.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid userId);
}

/// <summary>The portfolio in SQL Server (stored procedures in Obol.Database/StoredProcedures/Portfolio).</summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class PortfolioRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IPortfolioRepository
{
    /// <inheritdoc />
    public Task ReplaceHoldingsAsync(Guid userId, IReadOnlyCollection<HoldingInput> rows) =>
        ExecuteAsync("[dbo].[HoldingReplace]", new { UserUniqueID = userId, Rows = Json(rows) });

    /// <inheritdoc />
    public Task<List<HoldingRow>> ListHoldingsAsync(Guid userId) => QueryAsync<HoldingRow>("[dbo].[HoldingList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task ReplacePiesAsync(Guid userId, IReadOnlyCollection<PieInput> pies, IReadOnlyCollection<PieHoldingInput> holdings) =>
        ExecuteAsync("[dbo].[PieReplace]", new { UserUniqueID = userId, Pies = Json(pies), Holdings = Json(holdings) });

    /// <inheritdoc />
    public Task<List<PieRow>> ListPiesAsync(Guid userId) => QueryAsync<PieRow>("[dbo].[PieList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task<List<PieHoldingRow>> ListPieHoldingsAsync(Guid userId) => QueryAsync<PieHoldingRow>("[dbo].[PieHoldingList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public async Task<int> SaveTradesAsync(Guid userId, IReadOnlyCollection<TradeInput> rows) =>
        rows.Count == 0 ? 0 : await ExecuteScalarAsync<int>("[dbo].[TradeSave]", new { UserUniqueID = userId, Rows = Json(rows) });

    /// <inheritdoc />
    public Task<List<TradeRow>> ListTradesAsync(Guid userId) => QueryAsync<TradeRow>("[dbo].[TradeList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public async Task<int> SaveDividendsAsync(Guid userId, IReadOnlyCollection<DividendInput> rows) =>
        rows.Count == 0 ? 0 : await ExecuteScalarAsync<int>("[dbo].[DividendSave]", new { UserUniqueID = userId, Rows = Json(rows) });

    /// <inheritdoc />
    public Task<List<DividendRow>> ListDividendsAsync(Guid userId) => QueryAsync<DividendRow>("[dbo].[DividendList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public async Task<int> SaveCashAsync(Guid userId, IReadOnlyCollection<CashInput> rows) =>
        rows.Count == 0 ? 0 : await ExecuteScalarAsync<int>("[dbo].[CashMovementSave]", new { UserUniqueID = userId, Rows = Json(rows) });

    /// <inheritdoc />
    public Task<List<CashMovementRow>> ListCashAsync(Guid userId) => QueryAsync<CashMovementRow>("[dbo].[CashMovementList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task SaveSnapshotAsync(Guid userId, DateOnly day, SnapshotRow snapshot) =>
        ExecuteAsync("[dbo].[AccountSnapshotSave]", new
        {
            UserUniqueID = userId,
            Day = Date(day),
            snapshot.TotalValue,
            snapshot.Cash,
            snapshot.Cost,
            snapshot.UnrealizedPL,
            snapshot.RealizedPL,
        });

    /// <inheritdoc />
    public Task<List<SnapshotRow>> ListSnapshotsAsync(Guid userId) => QueryAsync<SnapshotRow>("[dbo].[AccountSnapshotList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId) => ExecuteAsync("[dbo].[PortfolioDelete]", new { UserUniqueID = userId });
}

/// <summary>The portfolio in demo mode (<see cref="MemoryStore"/>): same behaviour as the stored procedures.</summary>
/// <param name="store">The in-memory tables.</param>
/// <param name="timeProvider">The clock.</param>
internal sealed class MemoryPortfolioRepository(MemoryStore store, TimeProvider timeProvider) : IPortfolioRepository
{
    private long _nextId;

    /// <inheritdoc />
    public Task ReplaceHoldingsAsync(Guid userId, IReadOnlyCollection<HoldingInput> rows)
    {
        lock (store.Gate)
        {
            var book = Book(userId);
            book.Holdings.Clear();
            book.Holdings.AddRange(rows.Where(static row => row.Quantity > 0).Select(row => new HoldingRow
            {
                InstrumentID = row.InstrumentID,
                Quantity = row.Quantity,
                AveragePrice = row.AveragePrice,
                CurrentPrice = row.CurrentPrice,
                Value = Math.Round(row.Value, 2),
                Cost = Math.Round(row.Cost, 2),
                UnrealizedPL = Math.Round(row.UnrealizedPL, 2),
                FxImpact = row.FxImpact,
                OpenedAt = row.OpenedAt is { } opened ? DbTime.ParseTime(opened) : null,
                UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
            }));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<HoldingRow>> ListHoldingsAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(Book(userId).Holdings.ToList());
        }
    }

    /// <inheritdoc />
    public Task ReplacePiesAsync(Guid userId, IReadOnlyCollection<PieInput> pies, IReadOnlyCollection<PieHoldingInput> holdings)
    {
        lock (store.Gate)
        {
            var book = Book(userId);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            book.Pies.Clear();
            book.Pies.AddRange(pies.DistinctBy(static pie => pie.PieID).Select(pie => new PieRow
            {
                PieID = pie.PieID,
                Name = pie.Name.Length > 100 ? pie.Name[..100] : pie.Name,
                Icon = pie.Icon.Length > 40 ? pie.Icon[..40] : pie.Icon,
                Cash = Math.Round(pie.Cash, 2),
                Invested = Math.Round(pie.Invested, 2),
                Value = Math.Round(pie.Value, 2),
                Result = Math.Round(pie.Result, 2),
                DividendsGained = Math.Round(pie.DividendsGained, 2),
                Goal = pie.Goal,
                CreatedAt = pie.CreatedAt is { } created ? DbTime.ParseTime(created) : null,
                UpdatedAt = now,
            }));
            var ids = book.Pies.Select(static pie => pie.PieID).ToHashSet();
            book.PieHoldings.Clear();
            book.PieHoldings.AddRange(holdings.Where(holding => ids.Contains(holding.PieID))
                .GroupBy(static holding => (holding.PieID, holding.InstrumentID))
                .Select(static group => new PieHoldingRow
                {
                    PieID = group.Key.PieID,
                    InstrumentID = group.Key.InstrumentID,
                    Quantity = group.Sum(static holding => holding.Quantity),
                    Invested = Math.Round(group.Sum(static holding => holding.Invested), 2),
                    Value = Math.Round(group.Sum(static holding => holding.Value), 2),
                    Result = Math.Round(group.Sum(static holding => holding.Result), 2),
                    CurrentShare = group.Max(static holding => holding.CurrentShare),
                    ExpectedShare = group.Max(static holding => holding.ExpectedShare),
                }));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<PieRow>> ListPiesAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(Book(userId).Pies.ToList());
        }
    }

    /// <inheritdoc />
    public Task<List<PieHoldingRow>> ListPieHoldingsAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(Book(userId).PieHoldings.ToList());
        }
    }

    /// <inheritdoc />
    public Task<int> SaveTradesAsync(Guid userId, IReadOnlyCollection<TradeInput> rows)
    {
        lock (store.Gate)
        {
            var book = Book(userId);
            var added = 0;
            foreach (var row in rows)
            {
                if (book.Trades.Any(trade => trade.TradeKey == row.Key))
                {
                    continue;
                }

                book.Trades.Add(new TradeRow
                {
                    TradeID = Interlocked.Increment(ref _nextId),
                    InstrumentID = row.InstrumentID,
                    TradeKey = row.Key,
                    Side = row.Side,
                    Quantity = row.Quantity,
                    Price = row.Price,
                    PriceCurrency = row.PriceCurrency,
                    FxRate = row.FxRate,
                    Total = Math.Round(row.Total, 2),
                    RealizedPL = row.RealizedPL is { } realized ? Math.Round(realized, 2) : null,
                    Fees = Math.Round(row.Fees, 2),
                    ExecutedAt = DbTime.ParseTime(row.ExecutedAt),
                    Source = row.Source,
                    ExternalId = row.ExternalId,
                });
                added++;
            }

            book.Trades.Sort(static (a, b) => a.ExecutedAt.CompareTo(b.ExecutedAt));
            return Task.FromResult(added);
        }
    }

    /// <inheritdoc />
    public Task<List<TradeRow>> ListTradesAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(Book(userId).Trades.ToList());
        }
    }

    /// <inheritdoc />
    public Task<int> SaveDividendsAsync(Guid userId, IReadOnlyCollection<DividendInput> rows)
    {
        lock (store.Gate)
        {
            var book = Book(userId);
            var added = 0;
            foreach (var row in rows)
            {
                if (book.Dividends.Any(dividend => dividend.DividendKey == row.Key))
                {
                    continue;
                }

                book.Dividends.Add(new DividendRow
                {
                    DividendID = Interlocked.Increment(ref _nextId),
                    InstrumentID = row.InstrumentID,
                    DividendKey = row.Key,
                    PaidAt = DbTime.ParseTime(row.PaidAt),
                    Quantity = row.Quantity,
                    PerShare = row.PerShare,
                    Amount = Math.Round(row.Amount, 2),
                    Withheld = row.Withheld,
                    Kind = row.Kind,
                    Source = row.Source,
                });
                added++;
            }

            book.Dividends.Sort(static (a, b) => a.PaidAt.CompareTo(b.PaidAt));
            return Task.FromResult(added);
        }
    }

    /// <inheritdoc />
    public Task<List<DividendRow>> ListDividendsAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(Book(userId).Dividends.ToList());
        }
    }

    /// <inheritdoc />
    public Task<int> SaveCashAsync(Guid userId, IReadOnlyCollection<CashInput> rows)
    {
        lock (store.Gate)
        {
            var book = Book(userId);
            var added = 0;
            foreach (var row in rows.Where(static row => row.Kind is >= 1 and <= 5))
            {
                if (book.Cash.Any(cash => cash.MovementKey == row.Key))
                {
                    continue;
                }

                book.Cash.Add(new CashMovementRow
                {
                    CashMovementID = Interlocked.Increment(ref _nextId),
                    MovementKey = row.Key,
                    Kind = row.Kind,
                    Amount = Math.Round(row.Amount, 2),
                    At = DbTime.ParseTime(row.At),
                    Note = row.Note.Length > 100 ? row.Note[..100] : row.Note,
                    Source = row.Source,
                });
                added++;
            }

            book.Cash.Sort(static (a, b) => a.At.CompareTo(b.At));
            return Task.FromResult(added);
        }
    }

    /// <inheritdoc />
    public Task<List<CashMovementRow>> ListCashAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(Book(userId).Cash.ToList());
        }
    }

    /// <inheritdoc />
    public Task SaveSnapshotAsync(Guid userId, DateOnly day, SnapshotRow snapshot)
    {
        lock (store.Gate)
        {
            var book = Book(userId);
            book.Snapshots[day] = new SnapshotRow
            {
                Day = day.ToDateTime(TimeOnly.MinValue),
                TotalValue = snapshot.TotalValue,
                Cash = snapshot.Cash,
                Cost = snapshot.Cost,
                UnrealizedPL = snapshot.UnrealizedPL,
                RealizedPL = snapshot.RealizedPL,
            };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<SnapshotRow>> ListSnapshotsAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(Book(userId).Snapshots.Values.ToList());
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId)
    {
        lock (store.Gate)
        {
            store.Portfolios.Remove(userId);
            store.Runs.Remove(userId);
            if (store.Connections.TryGetValue(userId, out var connection))
            {
                connection.HistoryDoneAt = null;
                connection.State = 0;
                connection.LastMessage = string.Empty;
            }
        }

        return Task.CompletedTask;
    }

    private MemoryPortfolio Book(Guid userId) => MemoryStore.For(store.Portfolios, userId);
}
