using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Obol.Library.Enums;
using Obol.Library.Services;
using Obol.Library.Validation;
using Obol.WebApi.Extensions.Globalization;
using Obol.WebApi.Extensions.Security;
using Obol.WebApi.Extensions.Time;
using Obol.WebApi.Features.Connections.Models.DTOs;
using Obol.WebApi.Features.Connections.Models.Entities;
using Obol.WebApi.Features.Connections.Repositories;
using Obol.WebApi.Features.Csv;
using Obol.WebApi.Features.Logos.Services;
using Obol.WebApi.Features.Portfolio.Repositories;
using Obol.WebApi.Features.Portfolio.Services;
using Obol.WebApi.Features.Prices.Services;
using Obol.WebApi.Features.Syncs;
using Obol.WebApi.Features.Trading212;

namespace Obol.WebApi.Features.Connections.Services;

/// <summary>
/// The Sync screen: connect Trading 212, choose what to sync, sync now, import a CSV export, start over.
/// </summary>
internal interface IConnectionService
{
    /// <summary>The connection, its state and the last runs.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    Task<ServiceApiResponse<ConnectionDTO>> GetAsync(string userId);

    /// <summary>Checks a key and secret with Trading 212, saves them (encrypted) and starts the first sync.</summary>
    /// <param name="item">The account, key, secret and kind of account.</param>
    /// <returns>The connection, or validation problems.</returns>
    Task<ServiceApiResponse<ConnectionDTO>> ConnectAsync(ConnectRequest item);

    /// <summary>Saves what the sync reads.</summary>
    /// <param name="item">The choices.</param>
    /// <returns>The connection.</returns>
    Task<ServiceApiResponse<ConnectionDTO>> SetOptionsAsync(ConnectionOptionsRequest item);

    /// <summary>Starts a sync now (in the background).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection (state Running).</returns>
    Task<ServiceApiResponse<ConnectionDTO>> SyncNowAsync(string userId);

    /// <summary>Forgets the key and secret (the portfolio stays).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    Task<ServiceApiResponse<ConnectionDTO>> DisconnectAsync(string userId);

    /// <summary>Deletes everything imported, to start over.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    Task<ServiceApiResponse<ConnectionDTO>> DeleteDataAsync(string userId);

    /// <summary>Imports a Trading 212 CSV export.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="body">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>What was added, or validation problems.</returns>
    Task<ServiceApiResponse<CsvImportResultDTO>> ImportCsvAsync(string userId, Stream body, CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IConnectionService"/>.
/// </summary>
/// <param name="connectionRepository">Connections and the sync log.</param>
/// <param name="portfolioRepository">The portfolio.</param>
/// <param name="writer">Saves imports.</param>
/// <param name="client">Trading 212 (checks a new key).</param>
/// <param name="protector">Encrypts the key.</param>
/// <param name="syncQueue">The syncs waiting.</param>
/// <param name="priceQueue">The price history waiting.</param>
/// <param name="logoService">Whether logos are on.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="options">Module options.</param>
internal sealed class ConnectionService(
    IConnectionRepository connectionRepository,
    IPortfolioRepository portfolioRepository,
    PortfolioWriter writer,
    ITrading212Client client,
    ISecretProtector protector,
    SyncQueue syncQueue,
    PriceQueue priceQueue,
    LogoService logoService,
    IGlobalizationHelper globalizationHelper,
    IAppClock clock,
    IOptions<ObolOptions> options) : IConnectionService
{
    /// <summary>The largest CSV export accepted.</summary>
    internal const int MaxCsvBytes = 30 * 1024 * 1024;

    private const string FieldApiKey = "ApiKey";
    private const string FieldApiSecret = "ApiSecret";
    private const string FieldEnvironment = "Environment";
    private const string FieldFile = "File";

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ConnectionDTO>> GetAsync(string userId) =>
        Guid.TryParse(userId, out var id) ? ServiceApiResponse<ConnectionDTO>.Success(await BuildAsync(id)) : await NotFoundAsync<ConnectionDTO>(userId);

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ConnectionDTO>> ConnectAsync(ConnectRequest item)
    {
        if (!Guid.TryParse(item.UserId, out var id))
        {
            return await NotFoundAsync<ConnectionDTO>(item.UserId);
        }

        var key = (item.ApiKey ?? string.Empty).Trim();
        var secret = (item.ApiSecret ?? string.Empty).Trim();
        var environment = string.IsNullOrWhiteSpace(item.Environment) ? "live" : item.Environment.Trim().ToLowerInvariant();
        var errors = new List<ApiValidationError>();
        if (key.Length == 0)
        {
            errors.Add(await globalizationHelper.ValidationErrorAsync(FieldApiKey, nameof(TranslationKeys.RequiredField)));
        }

        if (secret.Length == 0)
        {
            errors.Add(await globalizationHelper.ValidationErrorAsync(FieldApiSecret, nameof(TranslationKeys.RequiredField)));
        }

        if (environment is not ("live" or "demo"))
        {
            errors.Add(await globalizationHelper.ValidationErrorAsync(FieldEnvironment, nameof(TranslationKeys.OutOfRange), "live", "demo"));
        }

        if (key.Length > 400 || secret.Length > 400)
        {
            errors.Add(await globalizationHelper.ValidationErrorAsync(FieldApiKey, nameof(TranslationKeys.PasswordTooLong)));
        }

        if (errors.Count > 0)
        {
            return (await ValidationProblemsAsync<ConnectionDTO>()).AddErrorRange(errors);
        }

        long? accountId = null;
        string? currency = null;
        if (!options.Value.DemoMode)
        {
            try
            {
                var summary = await client.AccountSummaryAsync(new Trading212Credentials(environment, key, secret), CancellationToken.None);
                accountId = summary.Id;
                currency = Trading212Mapper.Currency(summary.Currency);
            }
            catch (Trading212Exception exception)
            {
                var message = exception.Kind == Trading212Failure.Unauthorized
                    ? await globalizationHelper.TranslateAsync(nameof(TranslationKeys.BrokerKeyRejected))
                    : exception.Kind == Trading212Failure.RateLimited
                        ? await globalizationHelper.TranslateAsync(nameof(TranslationKeys.BrokerRateLimited))
                        : await globalizationHelper.TranslateAsync(nameof(TranslationKeys.BrokerUnreachable), exception.Message);
                return ServiceApiResponse<ConnectionDTO>.ValidationProblems(message).AddError(FieldApiKey, message);
            }
        }

        await connectionRepository.SaveAsync(new ConnectionRow
        {
            UserUniqueID = id,
            Environment = environment,
            ApiKeyCipher = protector.Protect(key),
            ApiSecretCipher = protector.Protect(secret),
            KeyHint = key.Length > 4 ? "••" + key[^4..] : "••••",
            AccountId = accountId,
            Currency = currency,
        });
        await StartSyncAsync(id);
        return ServiceApiResponse<ConnectionDTO>.Success(await BuildAsync(id));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ConnectionDTO>> SetOptionsAsync(ConnectionOptionsRequest item)
    {
        if (!Guid.TryParse(item.UserId, out var id))
        {
            return await NotFoundAsync<ConnectionDTO>(item.UserId);
        }

        await connectionRepository.SetOptionsAsync(id, item.SyncPositions, item.SyncOrders, item.SyncDividends, item.SyncTransactions);
        return ServiceApiResponse<ConnectionDTO>.Success(await BuildAsync(id));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ConnectionDTO>> SyncNowAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<ConnectionDTO>(userId);
        }

        if (await connectionRepository.GetAsync(id) is null)
        {
            var message = await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NotConnected));
            return ServiceApiResponse<ConnectionDTO>.ValidationProblems(message).AddError(FieldApiKey, message);
        }

        await StartSyncAsync(id);
        return ServiceApiResponse<ConnectionDTO>.Success(await BuildAsync(id));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ConnectionDTO>> DisconnectAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<ConnectionDTO>(userId);
        }

        await connectionRepository.DeleteAsync(id);
        return ServiceApiResponse<ConnectionDTO>.Success(await BuildAsync(id));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ConnectionDTO>> DeleteDataAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<ConnectionDTO>(userId);
        }

        await portfolioRepository.DeleteAsync(id);
        return ServiceApiResponse<ConnectionDTO>.Success(await BuildAsync(id));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<CsvImportResultDTO>> ImportCsvAsync(string userId, Stream body, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync<CsvImportResultDTO>(userId);
        }

        var started = clock.UtcNow;
        string text;
        using (var buffer = new MemoryStream())
        {
            var chunk = new byte[81920];
            int count;
            while ((count = await body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + count > MaxCsvBytes)
                {
                    return await CsvProblemAsync(nameof(TranslationKeys.CsvInvalid));
                }

                buffer.Write(chunk, 0, count);
            }

            text = Encoding.UTF8.GetString(buffer.ToArray());
        }

        var connection = await connectionRepository.GetAsync(id);
        var currency = connection?.Currency ?? options.Value.Currency;
        var batch = Trading212Csv.Parse(text, currency);
        if (batch is null)
        {
            return await CsvProblemAsync(nameof(TranslationKeys.CsvInvalid));
        }

        if (batch.IsEmpty)
        {
            var left = batch.SkippedKinds.Count == 0 ? string.Empty
                : " Left out: " + string.Join(", ", batch.SkippedKinds.OrderByDescending(static pair => pair.Value).Take(4).Select(static pair => $"{pair.Value} × {pair.Key}")) + ".";
            var message = await globalizationHelper.TranslateAsync(nameof(TranslationKeys.CsvEmpty)) + left;
            return ServiceApiResponse<CsvImportResultDTO>.ValidationProblems(message).AddError(FieldFile, message);
        }

        var counts = await writer.SaveAsync(id, batch);
        var read = batch.Trades.Count + batch.Dividends.Count + batch.Cash.Count;
        var added = counts.Trades + counts.Dividends + counts.Cash;
        var lines = new List<string>
        {
            $"{read} line{(read == 1 ? string.Empty : "s")} read: {batch.Trades.Count} trades, {batch.Dividends.Count} dividends, {batch.Cash.Count} transfers",
            $"{counts.Trades} new trades, {counts.Dividends} new dividends, {counts.Cash} new transfers",
        };
        if (read - added > 0)
        {
            lines.Add($"{read - added} already in Obol (imported before or synced)");
        }

        foreach (var (kind, count) in batch.SkippedKinds.OrderByDescending(static pair => pair.Value))
        {
            lines.Add($"Not kept: {count} × {kind}");
        }

        if (batch.Currency is { } fileCurrency && !string.Equals(fileCurrency, currency, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"The file is in {fileCurrency}; Obol shows this account in {currency} (OBOL_CURRENCY).");
        }

        var summary = added == 0 ? "CSV: nothing new" : $"CSV: {counts.Trades} trades, {counts.Dividends} dividends, {counts.Cash} transfers added";
        await connectionRepository.SaveRunAsync(id, new SyncRunRow
        {
            Source = (byte)DataSource.Csv,
            StartedAt = started,
            FinishedAt = clock.UtcNow,
            Ok = true,
            Summary = summary,
            Lines = JsonSerializer.Serialize(lines),
        });
        priceQueue.Enqueue(id);

        return ServiceApiResponse<CsvImportResultDTO>.Success(new CsvImportResultDTO
        {
            Trades = counts.Trades,
            Dividends = counts.Dividends,
            Cash = counts.Cash,
            Instruments = counts.Instruments,
            Duplicates = read - added,
            Skipped = batch.Skipped,
            Summary = summary,
            Lines = lines,
        });
    }

    private async Task StartSyncAsync(Guid id)
    {
        if (syncQueue.Enqueue(id))
        {
            await connectionRepository.MarkAsync(id, (byte)SyncState.Running, "Waiting to sync…");
        }
    }

    private async Task<ConnectionDTO> BuildAsync(Guid id)
    {
        var connection = await connectionRepository.GetAsync(id);
        var runs = await connectionRepository.ListRunsAsync(id, 10);
        var records = (await portfolioRepository.ListTradesAsync(id)).Count
            + (await portfolioRepository.ListDividendsAsync(id)).Count
            + (await portfolioRepository.ListCashAsync(id)).Count;
        var holdings = (await portfolioRepository.ListHoldingsAsync(id)).Count;
        var minutes = options.Value.SyncMinutes;
        var busy = syncQueue.IsBusy(id);
        return new ConnectionDTO
        {
            Connected = connection is not null,
            Environment = connection?.Environment,
            KeyHint = connection?.KeyHint,
            AccountId = connection?.AccountId,
            Currency = connection?.Currency ?? options.Value.Currency,
            SyncPositions = connection?.SyncPositions ?? true,
            SyncOrders = connection?.SyncOrders ?? true,
            SyncDividends = connection?.SyncDividends ?? true,
            SyncTransactions = connection?.SyncTransactions ?? true,
            State = busy ? SyncState.Running : connection is null ? SyncState.Never : (SyncState)connection.State == SyncState.Running ? SyncState.Ok : (SyncState)connection.State,
            Message = connection?.LastMessage ?? string.Empty,
            LastSyncAt = Utc(connection?.LastSyncAt),
            NextSyncAt = connection?.LastSyncAt is { } last && minutes > 0 ? Utc(last.AddMinutes(minutes)) : null,
            HistoryDone = connection?.HistoryDoneAt is not null,
            SyncMinutes = minutes,
            PriceHistory = options.Value.PriceHistory && !options.Value.DemoMode,
            Logos = logoService.Enabled,
            HasData = records + holdings > 0,
            Records = records,
            Runs = runs.Select(static run => new SyncRunDTO
            {
                Id = run.SyncRunID,
                Source = (DataSource)run.Source,
                StartedAt = Utc(run.StartedAt)!.Value,
                FinishedAt = Utc(run.FinishedAt)!.Value,
                Ok = run.Ok,
                Summary = run.Summary,
                Lines = Lines(run.Lines),
            }).ToList(),
        };
    }

    private static List<string> Lines(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DateTimeOffset? Utc(DateTime? value) =>
        value is { } time ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)) : null;

    private async Task<ServiceApiResponse<CsvImportResultDTO>> CsvProblemAsync(string key)
    {
        var message = await globalizationHelper.TranslateAsync(key);
        return ServiceApiResponse<CsvImportResultDTO>.ValidationProblems(message).AddError(FieldFile, message);
    }

    private async Task<ServiceApiResponse<T>> ValidationProblemsAsync<T>() =>
        ServiceApiResponse<T>.ValidationProblems(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)));

    private async Task<ServiceApiResponse<T>> NotFoundAsync<T>(string? what) =>
        ServiceApiResponse<T>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), what ?? string.Empty));
}
