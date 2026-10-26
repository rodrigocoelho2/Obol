using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Obol.WebApi.Features.Trading212;

/// <summary>
/// The Trading 212 public API (read-only calls). Every call carries the account's key and secret (HTTP Basic).
/// </summary>
internal interface ITrading212Client
{
    /// <summary>The account summary: id, currency, value, cash and investments.</summary>
    /// <param name="credentials">The key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The summary.</returns>
    Task<T212AccountSummary> AccountSummaryAsync(Trading212Credentials credentials, CancellationToken cancellationToken);

    /// <summary>The open positions.</summary>
    /// <param name="credentials">The key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The positions.</returns>
    Task<List<T212Position>> PositionsAsync(Trading212Credentials credentials, CancellationToken cancellationToken);

    /// <summary>Every instrument Trading 212 offers (kept for a day: the endpoint allows one call in 50 s).</summary>
    /// <param name="credentials">The key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The instruments, by ticker.</returns>
    Task<IReadOnlyDictionary<string, T212TradableInstrument>> InstrumentsAsync(Trading212Credentials credentials, CancellationToken cancellationToken);

    /// <summary>The filled orders, newest first, a page at a time.</summary>
    /// <param name="credentials">The key.</param>
    /// <param name="cancellationToken">Cancels the paging.</param>
    /// <returns>The pages.</returns>
    IAsyncEnumerable<List<T212HistoricalOrder>> OrdersAsync(Trading212Credentials credentials, CancellationToken cancellationToken);

    /// <summary>The dividends, newest first, a page at a time.</summary>
    /// <param name="credentials">The key.</param>
    /// <param name="cancellationToken">Cancels the paging.</param>
    /// <returns>The pages.</returns>
    IAsyncEnumerable<List<T212Dividend>> DividendsAsync(Trading212Credentials credentials, CancellationToken cancellationToken);

    /// <summary>Deposits, withdrawals, interest and fees, newest first, a page at a time.</summary>
    /// <param name="credentials">The key.</param>
    /// <param name="cancellationToken">Cancels the paging.</param>
    /// <returns>The pages.</returns>
    IAsyncEnumerable<List<T212Transaction>> TransactionsAsync(Trading212Credentials credentials, CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="ITrading212Client"/> over HttpClient. Keeps to the API's limits: the history endpoints allow 20 calls a
/// minute, so pages are 50 items and at least 3.2 s apart; a 429 waits until the reset the API announces and tries again.
/// </summary>
/// <param name="httpClientFactory">Makes the HttpClient.</param>
/// <param name="options">The API addresses.</param>
/// <param name="timeProvider">The clock (waits).</param>
/// <param name="logger">Logger (never logs a key).</param>
internal sealed class Trading212Client(
    IHttpClientFactory httpClientFactory,
    IOptions<Features.ObolOptions> options,
    TimeProvider timeProvider,
    ILogger<Trading212Client> logger) : ITrading212Client
{
    /// <summary>The name of the HttpClient.</summary>
    public const string HttpClientName = "trading212";

    private const int PageSize = 50;
    private const int MaxAttempts = 4;
    private static readonly TimeSpan PageGap = TimeSpan.FromMilliseconds(3200);
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(75);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim InstrumentsGate = new(1, 1);
    private static (DateTimeOffset At, IReadOnlyDictionary<string, T212TradableInstrument> Items)? instrumentsCache;

    /// <summary>Gets or sets the pause between history pages (tests set it to zero).</summary>
    internal TimeSpan Gap { get; set; } = PageGap;

    /// <inheritdoc />
    public Task<T212AccountSummary> AccountSummaryAsync(Trading212Credentials credentials, CancellationToken cancellationToken) =>
        GetAsync<T212AccountSummary>(credentials, "api/v0/equity/account/summary", cancellationToken);

    /// <inheritdoc />
    public Task<List<T212Position>> PositionsAsync(Trading212Credentials credentials, CancellationToken cancellationToken) =>
        GetAsync<List<T212Position>>(credentials, "api/v0/equity/positions", cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, T212TradableInstrument>> InstrumentsAsync(Trading212Credentials credentials, CancellationToken cancellationToken)
    {
        await InstrumentsGate.WaitAsync(cancellationToken);
        try
        {
            if (instrumentsCache is { } cached && timeProvider.GetUtcNow() - cached.At < TimeSpan.FromHours(24))
            {
                return cached.Items;
            }

            var list = await GetAsync<List<T212TradableInstrument>>(credentials, "api/v0/equity/metadata/instruments", cancellationToken);
            var items = list.Where(static item => !string.IsNullOrEmpty(item.Ticker))
                .GroupBy(static item => item.Ticker!, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
            instrumentsCache = (timeProvider.GetUtcNow(), items);
            return items;
        }
        finally
        {
            InstrumentsGate.Release();
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<List<T212HistoricalOrder>> OrdersAsync(Trading212Credentials credentials, CancellationToken cancellationToken) =>
        PagesAsync<T212HistoricalOrder>(credentials, $"api/v0/equity/history/orders?limit={PageSize}", cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<List<T212Dividend>> DividendsAsync(Trading212Credentials credentials, CancellationToken cancellationToken) =>
        PagesAsync<T212Dividend>(credentials, $"api/v0/equity/history/dividends?limit={PageSize}", cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<List<T212Transaction>> TransactionsAsync(Trading212Credentials credentials, CancellationToken cancellationToken) =>
        PagesAsync<T212Transaction>(credentials, $"api/v0/equity/history/transactions?limit={PageSize}", cancellationToken);

    private async IAsyncEnumerable<List<T>> PagesAsync<T>(Trading212Credentials credentials, string firstPath, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? path = firstPath;
        var pages = 0;
        while (path is not null && pages < 2000)
        {
            if (pages > 0 && Gap > TimeSpan.Zero)
            {
                await Task.Delay(Gap, timeProvider, cancellationToken);
            }

            var page = await GetAsync<T212Page<T>>(credentials, path, cancellationToken);
            pages++;
            yield return page.Items ?? [];
            path = string.IsNullOrWhiteSpace(page.NextPagePath) ? null : page.NextPagePath.TrimStart('/');
        }
    }

    private async Task<T> GetAsync<T>(Trading212Credentials credentials, string path, CancellationToken cancellationToken)
    {
        var baseUrl = string.Equals(credentials.Environment, "demo", StringComparison.OrdinalIgnoreCase)
            ? options.Value.Trading212DemoUrl
            : options.Value.Trading212LiveUrl;
        var url = new Uri(new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/"), path);
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.ApiKey.Trim()}:{credentials.ApiSecret.Trim()}"));
        var client = httpClientFactory.CreateClient(HttpClientName);

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorization);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                if (attempt >= MaxAttempts)
                {
                    throw new Trading212Exception(Trading212Failure.Unreachable, exception is TaskCanceledException ? "timed out" : exception.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), timeProvider, cancellationToken);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    try
                    {
                        return (await JsonSerializer.DeserializeAsync<T>(stream, Json, cancellationToken))
                            ?? throw new Trading212Exception(Trading212Failure.Unexpected, $"empty answer from {path}");
                    }
                    catch (JsonException exception)
                    {
                        throw new Trading212Exception(Trading212Failure.Unexpected, $"unreadable answer from {Path(path)} ({exception.Message})");
                    }
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new Trading212Exception(
                        response.StatusCode == HttpStatusCode.Forbidden ? Trading212Failure.Forbidden : Trading212Failure.Unauthorized,
                        $"{(int)response.StatusCode} on {Path(path)}");
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    if (attempt >= MaxAttempts)
                    {
                        throw new Trading212Exception(
                            response.StatusCode == HttpStatusCode.TooManyRequests ? Trading212Failure.RateLimited : Trading212Failure.Unreachable,
                            $"{(int)response.StatusCode} on {Path(path)}");
                    }

                    var wait = WaitFor(response);
                    if (logger.IsEnabled(LogLevel.Information))
                        logger.LogInformation("Trading 212 answered {Status} on {Path}; waiting {Seconds:0.#} s", (int)response.StatusCode, Path(path), wait.TotalSeconds);

                    await Task.Delay(wait, timeProvider, cancellationToken);
                    continue;
                }

                throw new Trading212Exception(Trading212Failure.Unexpected, $"{(int)response.StatusCode} on {Path(path)}");
            }
        }
    }

    private TimeSpan WaitFor(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-ratelimit-reset", out var values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var reset))
        {
            var until = DateTimeOffset.FromUnixTimeSeconds(reset) - timeProvider.GetUtcNow() + TimeSpan.FromMilliseconds(500);
            return until <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : until > MaxWait ? MaxWait : until;
        }

        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta > MaxWait ? MaxWait : delta;
        }

        return TimeSpan.FromSeconds(10);
    }

    private static string Path(string path)
    {
        var query = path.IndexOf('?', StringComparison.Ordinal);
        return query < 0 ? path : path[..query];
    }
}
