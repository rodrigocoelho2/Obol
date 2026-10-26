using System.Globalization;
using System.Text.Json;
using Obol.Library.Enums;
using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Extensions.Security;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features.Connections.Models.Entities;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Instruments.Repositories;
using Obol.WebApi.Features.Portfolio.Models.Entities;
using Obol.WebApi.Features.Portfolio.Repositories;
using Obol.WebApi.Features.Portfolio.Services;
using Obol.WebApi.Features.Prices.Repositories;
using Obol.WebApi.Features.Prices.Services;

namespace Obol.WebApi.Features.Trading212;

/// <summary>
/// Runs one sync of an account (in the background, from <see cref="Syncs.SyncWorker"/>).
/// </summary>
internal interface ISyncRunner
{
    /// <summary>
    /// Syncs an account and logs the run.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Stops the sync (the app is shutting down).</param>
    /// <returns>True when it worked.</returns>
    Task<bool> RunAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// One Trading 212 sync: the account summary, the open positions (with today's prices and exchange rates), then the
/// order, dividend and transaction history. The first sync reads the whole history; later ones stop at the first page
/// with nothing new.
/// </summary>
/// <param name="client">The Trading 212 API.</param>
/// <param name="connectionRepository">Connections and the sync log.</param>
/// <param name="portfolioRepository">The portfolio.</param>
/// <param name="instrumentRepository">Instruments.</param>
/// <param name="priceRepository">Prices.</param>
/// <param name="writer">Saves what was read.</param>
/// <param name="protector">Decrypts the key.</param>
/// <param name="priceQueue">Asks for the price history of new instruments.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="logger">Logger.</param>
internal sealed class Trading212Sync(
    ITrading212Client client,
    IConnectionRepository connectionRepository,
    IPortfolioRepository portfolioRepository,
    IInstrumentRepository instrumentRepository,
    IPriceRepository priceRepository,
    PortfolioWriter writer,
    ISecretProtector protector,
    PriceQueue priceQueue,
    IAppClock clock,
    ILogger<Trading212Sync> logger) : ISyncRunner
{
    /// <summary>The permission a key needs to read the pies.</summary>
    internal const string PiesPermission = "Pies - Read";

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    /// <inheritdoc />
    public async Task<bool> RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await connectionRepository.GetAsync(userId);
        if (connection is null)
        {
            return false;
        }

        var started = clock.UtcNow;
        var lines = new List<string>();
        try
        {
            var credentials = Credentials(connection);
            await connectionRepository.MarkAsync(userId, (byte)SyncState.Running, "Syncing…");
            var (summary, historyDone) = await SyncAsync(userId, connection, credentials, lines, cancellationToken);
            var finished = clock.UtcNow;
            await connectionRepository.MarkAsync(userId, (byte)SyncState.Ok, summary, finished, historyDone ? finished : null, null, null);
            await connectionRepository.SaveRunAsync(userId, Run(started, finished, true, summary, lines));
            priceQueue.Enqueue(userId);
            return true;
        }
        catch (MissingPermissionException exception)
        {
            var message = $"Synced what the key allows. In Trading 212 (Settings › API) turn on {string.Join(", ", exception.Permissions.Select(static permission => $"\"{permission}\""))} for this key, or make a new key with them, and paste it in Sync.";
            lines.Add(message);
            var finished = clock.UtcNow;
            await connectionRepository.MarkAsync(userId, (byte)SyncState.Failed, message, finished, null, null, null);
            await connectionRepository.SaveRunAsync(userId, Run(started, finished, false, message, lines));
            priceQueue.Enqueue(userId);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await connectionRepository.MarkAsync(userId, (byte)SyncState.Failed, "Stopped (Obol was restarting). It runs again on its own.");
            throw;
        }
        catch (Exception exception) when (exception is Trading212Exception or InvalidOperationException or HttpRequestException or JsonException)
        {
            var message = exception switch
            {
                Trading212Exception { Kind: Trading212Failure.Unauthorized } => "Trading 212 refused the key (check it's the Real money / Practice account it was made for). Paste a new key and secret in Sync.",
                Trading212Exception { Kind: Trading212Failure.Forbidden } => "The key doesn't have the \"Account data\" permission. Turn it on in Trading 212 (Settings › API) and paste the key again.",
                Trading212Exception { Kind: Trading212Failure.RateLimited } => "Trading 212 asked to slow down. Obol tries again at the next sync.",
                Trading212Exception { Kind: Trading212Failure.Unreachable } failure => $"Can't reach Trading 212 ({failure.Message}).",
                _ => $"The sync stopped: {exception.Message}",
            };
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Sync of {UserId} failed: {Message}", userId, exception.Message);

            lines.Add(message);
            var finished = clock.UtcNow;
            await connectionRepository.MarkAsync(userId, (byte)SyncState.Failed, message);
            await connectionRepository.SaveRunAsync(userId, Run(started, finished, false, message, lines));
            return false;
        }
    }

    private Trading212Credentials Credentials(ConnectionRow connection)
    {
        var key = protector.Unprotect(connection.ApiKeyCipher);
        var secret = protector.Unprotect(connection.ApiSecretCipher);
        if (key is null || secret is null)
        {
            throw new InvalidOperationException("the saved key can't be read (was OBOL_ENCRYPTION_KEY changed?). Paste the key and secret again");
        }

        return new Trading212Credentials(connection.Environment, key, secret);
    }

    private async Task<(string Summary, bool HistoryDone)> SyncAsync(
        Guid userId, ConnectionRow connection, Trading212Credentials credentials, List<string> lines, CancellationToken cancellationToken)
    {
        var account = await client.AccountSummaryAsync(credentials, cancellationToken);
        var currency = Trading212Mapper.Currency(account.Currency ?? connection.Currency);
        await connectionRepository.MarkAsync(userId, (byte)SyncState.Running, "Syncing…", accountId: account.Id, currency: currency);
        lines.Add($"Account {account.Id} · {currency} · worth {Money(account.TotalValue, currency)}");

        IReadOnlyDictionary<string, T212TradableInstrument>? catalogue = null;
        try
        {
            catalogue = await client.InstrumentsAsync(credentials, cancellationToken);
        }
        catch (Trading212Exception exception)
        {
            lines.Add($"Instrument list not read ({exception.Message}): ETFs and shares are told apart by name.");
        }

        var parts = new List<string>();
        var missing = new List<string>();
        var today = clock.Today;
        Dictionary<string, int>? byTicker = null;

        if (connection.SyncPositions
            && await AllowedAsync(() => client.PositionsAsync(credentials, cancellationToken), "Portfolio", "Positions", missing, lines) is { } positions)
        {
            var instruments = new Dictionary<string, InstrumentInput>(StringComparer.Ordinal);
            foreach (var position in positions)
            {
                if (Trading212Mapper.Instrument(position.Instrument, null, null, catalogue) is { } instrument)
                {
                    instruments.TryAdd(instrument.Key, instrument);
                }
            }

            var saved = (await instrumentRepository.UpsertAsync(instruments.Values.ToList())).ToDictionary(static row => row.InstrumentKey, StringComparer.Ordinal);
            byTicker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var holdings = new List<HoldingInput>();
            var prices = new List<PriceInput>();
            var rates = new List<FxInput>();
            foreach (var position in positions)
            {
                var input = Trading212Mapper.Instrument(position.Instrument, null, null, catalogue);
                if (input is null || !saved.TryGetValue(input.Key, out var row) || position.Quantity <= 0)
                {
                    continue;
                }

                if (position.Instrument?.Ticker is { Length: > 0 } ticker)
                {
                    byTicker.TryAdd(ticker.Trim(), row.InstrumentID);
                }

                var wallet = position.WalletImpact;
                holdings.Add(new HoldingInput(
                    row.InstrumentID, position.Quantity, position.AveragePricePaid, position.CurrentPrice,
                    wallet?.CurrentValue ?? 0m, wallet?.TotalCost ?? 0m, wallet?.UnrealizedProfitLoss ?? 0m, wallet?.FxImpact,
                    position.CreatedAt is { } created ? DbTime.Time(created.UtcDateTime) : null));
                // At the weekend the price is Friday's close (crypto trades every day).
                var priceDay = row.Class == AssetClass.Crypto ? today : PortfolioLoader.MarketDay(today);
                prices.Add(new PriceInput(row.InstrumentID, DbTime.Day(priceDay), position.CurrentPrice, (byte)DataSource.Trading212));
                if (!string.Equals(row.Currency, currency, StringComparison.OrdinalIgnoreCase) && wallet is { CurrentValue: > 0 } && position.CurrentPrice > 0)
                {
                    var rate = wallet.CurrentValue / (position.Quantity * position.CurrentPrice);
                    var fxCurrency = row.Currency == "GBX" ? "GBP" : row.Currency;
                    rates.Add(new FxInput(fxCurrency, currency, DbTime.Day(PortfolioLoader.MarketDay(today)), row.Currency == "GBX" ? rate * 100m : rate));
                }
            }

            await portfolioRepository.ReplaceHoldingsAsync(userId, holdings);
            await priceRepository.SavePricesAsync(prices);
            await priceRepository.SaveFxAsync(rates);

            var cash = (account.Cash?.AvailableToTrade ?? 0m) + (account.Cash?.ReservedForOrders ?? 0m) + (account.Cash?.InPies ?? 0m);
            await portfolioRepository.SaveSnapshotAsync(userId, today, new SnapshotRow
            {
                TotalValue = account.TotalValue,
                Cash = cash,
                Cost = account.Investments?.TotalCost ?? holdings.Sum(static holding => holding.Cost),
                UnrealizedPL = account.Investments?.UnrealizedProfitLoss ?? holdings.Sum(static holding => holding.UnrealizedPL),
                RealizedPL = account.Investments?.RealizedProfitLoss ?? 0m,
            });
            lines.Add($"{holdings.Count} position{(holdings.Count == 1 ? string.Empty : "s")} · cash {Money(cash, currency)}");
            parts.Add($"{holdings.Count} position{(holdings.Count == 1 ? string.Empty : "s")}");
        }

        if (byTicker is not null)
        {
            if (await SyncPiesAsync(userId, credentials, byTicker, catalogue, lines, cancellationToken) is { } pieCount)
            {
                parts.Add(Count(pieCount, "pie"));
            }
        }

        var fullHistory = connection.HistoryDoneAt is null;
        var historyDone = true;

        if (connection.SyncOrders)
        {
            var known = (await portfolioRepository.ListTradesAsync(userId)).Select(static trade => trade.TradeKey).ToHashSet(StringComparer.Ordinal);
            var (added, pages, complete) = await HistoryAsync(
                client.OrdersAsync(credentials, cancellationToken), fullHistory, userId, "History - Orders", "Orders", missing, lines,
                (batch, item) =>
                {
                    var instrument = Trading212Mapper.Instrument(item.Order?.Instrument, item.Order?.Ticker, item.Order?.Currency, catalogue);
                    if (instrument is null)
                    {
                        batch.Skip("order without an instrument");
                        return false;
                    }

                    var trade = Trading212Mapper.Trade(item, instrument.Key, instrument.Currency);
                    if (trade is null)
                    {
                        batch.Skip(item.Fill?.Type is { Length: > 0 } type && type != "TRADE" ? type.ToLowerInvariant().Replace('_', ' ') : "unfilled order");
                        return false;
                    }

                    batch.Add(instrument);
                    batch.Trades.Add(trade);
                    return !known.Contains(PortfolioWriter.TradeKey(trade));
                },
                static counts => counts.Trades);
            historyDone &= complete;
            if (pages >= 0)
            {
                lines.Add($"Orders: {pages} page{(pages == 1 ? string.Empty : "s")}, {Count(added, "new trade")}");
                parts.Add(Count(added, "new trade"));
            }
        }

        if (connection.SyncDividends)
        {
            var known = (await portfolioRepository.ListDividendsAsync(userId)).Select(static dividend => dividend.DividendKey).ToHashSet(StringComparer.Ordinal);
            var (added, pages, complete) = await HistoryAsync(
                client.DividendsAsync(credentials, cancellationToken), fullHistory, userId, "History - Dividends", "Dividends", missing, lines,
                (batch, item) =>
                {
                    var instrument = Trading212Mapper.Instrument(item.Instrument, item.Ticker, item.TickerCurrency, catalogue);
                    if (instrument is null)
                    {
                        batch.Skip("dividend without an instrument");
                        return false;
                    }

                    var dividend = Trading212Mapper.Dividend(item, instrument.Key);
                    batch.Add(instrument);
                    batch.Dividends.Add(dividend);
                    return !known.Contains(PortfolioWriter.DividendKey(dividend));
                },
                static counts => counts.Dividends);
            historyDone &= complete;
            if (pages >= 0)
            {
                lines.Add($"Dividends: {pages} page{(pages == 1 ? string.Empty : "s")}, {Count(added, "new dividend")}");
                parts.Add(Count(added, "new dividend"));
            }
        }

        if (connection.SyncTransactions)
        {
            var known = (await portfolioRepository.ListCashAsync(userId)).Select(static cash => cash.MovementKey).ToHashSet(StringComparer.Ordinal);
            var (added, pages, complete) = await HistoryAsync(
                client.TransactionsAsync(credentials, cancellationToken), fullHistory, userId, "History - Transactions", "Deposits, withdrawals and interest", missing, lines,
                (batch, item) =>
                {
                    var cash = Trading212Mapper.Cash(item);
                    if (cash is null)
                    {
                        batch.Skip((item.Type ?? "unknown").ToLowerInvariant().Replace('_', ' '));
                        return false;
                    }

                    batch.Cash.Add(cash);
                    return !known.Contains(PortfolioWriter.CashKey(cash));
                },
                static counts => counts.Cash);
            historyDone &= complete;
            if (pages >= 0)
            {
                lines.Add($"Deposits, withdrawals and interest: {pages} page{(pages == 1 ? string.Empty : "s")}, {Count(added, "new transfer")}");
                parts.Add(Count(added, "new transfer"));
            }
        }

        if (missing.Count > 0)
        {
            throw new MissingPermissionException(missing);
        }

        return (parts.Count == 0 ? "Nothing to sync (everything is turned off)." : string.Join(" · ", parts), historyDone);
    }

    /// <summary>
    /// Reads the pies and what each owns, and replaces the ones kept. Pies are optional: a key without "Pies - Read", or
    /// Trading 212 failing to answer, leaves the pies of the last sync and only adds a line to the log.
    /// </summary>
    private async Task<int?> SyncPiesAsync(
        Guid userId, Trading212Credentials credentials, Dictionary<string, int> byTicker, IReadOnlyDictionary<string, T212TradableInstrument>? catalogue,
        List<string> lines, CancellationToken cancellationToken)
    {
        List<T212Pie> pies;
        try
        {
            pies = await client.PiesAsync(credentials, cancellationToken);
        }
        catch (Trading212Exception exception) when (exception.Kind is Trading212Failure.Unauthorized or Trading212Failure.Forbidden)
        {
            lines.Add($"Pies: not read. To see your holdings grouped by pie, turn on \"{PiesPermission}\" for this key in Trading 212");
            return null;
        }
        catch (Trading212Exception exception)
        {
            lines.Add($"Pies: not read this time ({exception.Message}), the last ones are kept");
            return null;
        }

        var rows = new List<PieInput>();
        var owned = new List<PieHoldingInput>();
        var unread = new HashSet<long>();
        var unmatched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var details = new Dictionary<long, T212PieDetail>();
        foreach (var pie in pies)
        {
            T212PieDetail? detail = null;
            try
            {
                detail = await client.PieAsync(credentials, pie.Id, cancellationToken);
            }
            catch (Trading212Exception exception)
            {
                unread.Add(pie.Id);
                lines.Add($"Pie {pie.Id}: details not read ({exception.Message})");
            }

            rows.Add(Trading212Mapper.Pie(pie, detail));
            if (detail is not null)
            {
                details[pie.Id] = detail;
                owned.AddRange(Trading212Mapper.PieHoldings(pie.Id, detail, ticker => byTicker.TryGetValue(ticker, out var id) ? id : null));
                foreach (var ticker in (detail.Instruments ?? []).Where(item => item is { OwnedQuantity: > 0, Ticker.Length: > 0 } && !byTicker.ContainsKey(item.Ticker!.Trim())).Select(static item => item.Ticker!.Trim()))
                {
                    unmatched.Add(ticker);
                }
            }
        }

        if (unmatched.Count > 0)
        {
            // A ticker the positions spelt differently: find its instrument by ISIN / key, as the history import keys it.
            var keys = (await instrumentRepository.ListAsync()).ToDictionary(static row => row.InstrumentKey, static row => row.InstrumentID, StringComparer.Ordinal);
            var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var ticker in unmatched)
            {
                if (Trading212Mapper.Instrument(null, ticker, null, catalogue) is { } input && keys.TryGetValue(input.Key, out var id))
                {
                    found[ticker] = id;
                }
            }

            foreach (var pie in pies)
            {
                if (details.TryGetValue(pie.Id, out var detail))
                {
                    owned.AddRange(Trading212Mapper.PieHoldings(pie.Id, detail, ticker => found.TryGetValue(ticker, out var id) ? id : null));
                }
            }

            var missed = unmatched.Count - found.Count;
            if (missed > 0)
            {
                lines.Add($"Pies: {Count(missed, "holding")} not matched to a position ({string.Join(", ", unmatched.Where(ticker => !found.ContainsKey(ticker)).Take(5))})");
            }
        }

        if (unread.Count > 0 && unread.Count == pies.Count)
        {
            lines.Add("Pies: Trading 212 didn't give their details this time, the last ones are kept");
            return null;
        }

        if (unread.Count > 0)
        {
            // A pie whose details didn't come keeps its name and what it owned at the last sync.
            var before = (await portfolioRepository.ListPiesAsync(userId)).ToDictionary(static pie => pie.PieID);
            for (var i = 0; i < rows.Count; i++)
            {
                if (unread.Contains(rows[i].PieID) && before.TryGetValue(rows[i].PieID, out var known))
                {
                    rows[i] = rows[i] with { Name = known.Name, Icon = known.Icon, Goal = known.Goal, CreatedAt = known.CreatedAt is { } made ? DbTime.Time(made) : null };
                }
            }

            owned.AddRange((await portfolioRepository.ListPieHoldingsAsync(userId)).Where(row => unread.Contains(row.PieID)).Select(static row =>
                new PieHoldingInput(row.PieID, row.InstrumentID, row.Quantity, row.Invested, row.Value, row.Result, row.CurrentShare, row.ExpectedShare)));
        }

        await portfolioRepository.ReplacePiesAsync(userId, rows, owned);
        lines.Add(pies.Count == 0 ? "Pies: none" : $"{Count(pies.Count, "pie")} · {Count(owned.Count, "holding")} in them{(unread.Count > 0 ? $" ({unread.Count} not read this time)" : string.Empty)}");
        return pies.Count;
    }

    /// <summary>
    /// Calls an endpoint the key may not be allowed to use (Trading 212 keys have one permission per area): a refusal
    /// skips that part of the sync instead of stopping it, and is reported at the end.
    /// </summary>
    private static async Task<T?> AllowedAsync<T>(Func<Task<T>> call, string permission, string what, List<string> missing, List<string> lines)
        where T : class
    {
        try
        {
            return await call();
        }
        catch (Trading212Exception exception) when (exception.Kind is Trading212Failure.Unauthorized or Trading212Failure.Forbidden)
        {
            missing.Add(permission);
            lines.Add($"{what}: skipped, the key doesn't have the \"{permission}\" permission");
            return null;
        }
    }

    /// <summary>
    /// Reads a history endpoint page by page and saves each page. On a full read every page is read; otherwise the read
    /// stops at the first page with nothing new (the pages come newest first).
    /// </summary>
    private async Task<(int Added, int Pages, bool Complete)> HistoryAsync<T>(
        IAsyncEnumerable<List<T>> source, bool full, Guid userId, string permission, string what, List<string> missing, List<string> lines,
        Func<ImportBatch, T, bool> read, Func<ImportCounts, int> added)
    {
        try
        {
            return await ReadHistoryAsync(source, full, userId, read, added);
        }
        catch (Trading212Exception exception) when (exception.Kind is Trading212Failure.Unauthorized or Trading212Failure.Forbidden)
        {
            missing.Add(permission);
            lines.Add($"{what}: skipped, the key doesn't have the \"{permission}\" permission");
            return (0, -1, false);
        }
    }

    private async Task<(int Added, int Pages, bool Complete)> ReadHistoryAsync<T>(
        IAsyncEnumerable<List<T>> source, bool full, Guid userId, Func<ImportBatch, T, bool> read, Func<ImportCounts, int> added)
    {
        var total = 0;
        var pages = 0;
        await foreach (var page in source)
        {
            pages++;
            var batch = new ImportBatch();
            var fresh = 0;
            foreach (var item in page)
            {
                if (read(batch, item))
                {
                    fresh++;
                }
            }

            if (!batch.IsEmpty)
            {
                total += added(await writer.SaveAsync(userId, batch));
            }

            if (!full && fresh == 0)
            {
                return (total, pages, true);
            }
        }

        return (total, pages, true);
    }

    private static SyncRunRow Run(DateTime started, DateTime finished, bool ok, string summary, List<string> lines) => new()
    {
        Source = (byte)DataSource.Trading212,
        StartedAt = started,
        FinishedAt = finished,
        Ok = ok,
        Summary = summary,
        Lines = JsonSerializer.Serialize(lines),
    };

    private static string Count(int count, string what) => count == 0 ? $"no {what}s" : count == 1 ? $"1 {what}" : $"{count} {what}s";

    private static string Money(decimal value, string currency) => $"{value.ToString("#,0.00", English)} {currency}";
}

/// <summary>Some parts of a sync were skipped because the key lacks their permission.</summary>
/// <param name="permissions">The permissions to turn on, as Trading 212 names them.</param>
internal sealed class MissingPermissionException(IReadOnlyList<string> permissions) : Exception("the key lacks some permissions")
{
    /// <summary>Gets the permissions to turn on.</summary>
    public IReadOnlyList<string> Permissions { get; } = permissions.Distinct().ToList();
}
