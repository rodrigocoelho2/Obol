using System.Globalization;
using System.Text.Json;

namespace Obol.WebApi.Features.Prices.Services;

/// <summary>A quote Yahoo Finance found for a search.</summary>
/// <param name="Symbol">The Yahoo symbol (IWDA.AS).</param>
/// <param name="Exchange">The exchange code (AMS, NMS…).</param>
/// <param name="Type">EQUITY, ETF…</param>
internal sealed record YahooQuote(string Symbol, string Exchange, string Type, string Sector = "", string Industry = "");

/// <summary>Daily closes of a symbol.</summary>
/// <param name="Currency">The currency Yahoo prices it in (GBp for pence).</param>
/// <param name="Closes">The closes, oldest first.</param>
internal sealed record YahooChart(string Currency, IReadOnlyList<KeyValuePair<DateOnly, decimal>> Closes);

/// <summary>
/// Daily closing prices and exchange rates from Yahoo Finance (the public chart and search endpoints). Best effort:
/// any failure returns nothing and the charts fall back to the trades and the syncs.
/// </summary>
internal interface IYahooClient
{
    /// <summary>Finds the listings of an ISIN.</summary>
    /// <param name="query">The ISIN (or a symbol).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The quotes found.</returns>
    Task<List<YahooQuote>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>Gets the daily closes of a symbol between two days.</summary>
    /// <param name="symbol">The Yahoo symbol (or USDEUR=X for a rate).</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The chart, or null when Yahoo has none.</returns>
    Task<YahooChart?> ChartAsync(string symbol, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IYahooClient"/> over HttpClient.
/// </summary>
/// <param name="httpClientFactory">Makes the HttpClient.</param>
/// <param name="logger">Logger.</param>
internal sealed class YahooClient(IHttpClientFactory httpClientFactory, ILogger<YahooClient> logger) : IYahooClient
{
    /// <summary>The name of the HttpClient.</summary>
    public const string HttpClientName = "yahoo";

    /// <inheritdoc />
    public async Task<List<YahooQuote>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            $"https://query2.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=8&newsCount=0&listsCount=0", cancellationToken);
        var quotes = new List<YahooQuote>();
        if (document?.RootElement.TryGetProperty("quotes", out var items) == true && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var symbol = Text(item, "symbol");
                if (symbol.Length > 0)
                {
                    quotes.Add(new YahooQuote(
                        symbol, Text(item, "exchange"), Text(item, "quoteType"),
                        FirstText(item, "sectorDisp", "sector"), FirstText(item, "industryDisp", "industry")));
                }
            }
        }

        return quotes;
    }

    /// <inheritdoc />
    public async Task<YahooChart?> ChartAsync(string symbol, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        using var document = await GetJsonAsync(
            $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?period1={start}&period2={end}&interval=1d&includePrePost=false",
            cancellationToken);
        if (document is null
            || !document.RootElement.TryGetProperty("chart", out var chart)
            || !chart.TryGetProperty("result", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            return null;
        }

        var result = results[0];
        var currency = result.TryGetProperty("meta", out var meta) ? Text(meta, "currency") : string.Empty;
        var offset = meta.ValueKind == JsonValueKind.Object && meta.TryGetProperty("gmtoffset", out var gmt) && gmt.TryGetInt64(out var seconds) ? seconds : 0;
        if (!result.TryGetProperty("timestamp", out var stamps) || stamps.ValueKind != JsonValueKind.Array
            || !result.TryGetProperty("indicators", out var indicators) || !indicators.TryGetProperty("quote", out var quote)
            || quote.ValueKind != JsonValueKind.Array || quote.GetArrayLength() == 0 || !quote[0].TryGetProperty("close", out var closes))
        {
            return new YahooChart(currency, []);
        }

        var days = new SortedDictionary<DateOnly, decimal>();
        var count = Math.Min(stamps.GetArrayLength(), closes.GetArrayLength());
        for (var i = 0; i < count; i++)
        {
            if (stamps[i].TryGetInt64(out var stamp) && closes[i].ValueKind == JsonValueKind.Number && closes[i].TryGetDecimal(out var close) && close > 0)
            {
                // The exchange's own day (a 22:00 UTC close in New York is still that day).
                days[DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(stamp + offset).UtcDateTime)] = close;
            }
        }

        return new YahooChart(currency, days.ToList());
    }

    private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Yahoo answered {Status} for {Url}", (int)response.StatusCode, url);

                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Yahoo Finance not reachable ({Message})", exception.Message);

            return null;
        }
    }

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>Formats a day for logs.</summary>
    /// <param name="day">The day.</param>
    /// <returns>yyyy-MM-dd.</returns>
    private static string FirstText(JsonElement element, params string[] names) =>
        names.Select(name => Text(element, name)).FirstOrDefault(static value => value.Length > 0) ?? string.Empty;

    internal static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
