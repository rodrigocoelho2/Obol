using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Obol.Library.Enums;
using Obol.Library.Validation;
using Obol.Sdk.Models;

namespace Obol.Sdk;

/// <summary>
/// Typed client of Obol.WebApi. Method names follow the API operation ids ({Feature}{Operation}Async).
/// Register with <see cref="ServiceCollectionExtensions.AddObolClient"/>, which sets the base address and the x-api-key header.
/// </summary>
/// <param name="httpClient">The configured HttpClient.</param>
public sealed class ObolClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    // ----- User -----

    /// <summary>How many accounts exist, and the demo account in demo mode.</summary>
    /// <returns>The status.</returns>
    public Task<UserStatusDTO> UserStatusAsync() => GetAsync<UserStatusDTO>("user/status");

    /// <summary>Lists every account.</summary>
    /// <returns>The accounts.</returns>
    public Task<List<UserDTO>> UserListAsync() => GetAsync<List<UserDTO>>("user/list");

    /// <summary>Gets one account.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserGetAsync(string identifier) => GetAsync<UserDTO>($"user?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Creates an account (the first one is an admin).</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserCreateAsync(UserCreateRequest item) => PostAsync<UserDTO>("user", item);

    /// <summary>Checks a user name and password.</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserSignInAsync(UserSignInRequest item) => PostAsync<UserDTO>("user/signin", item);

    /// <summary>Changes a password.</summary>
    /// <param name="item">The account, current and new password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserPasswordAsync(UserPasswordRequest item) => PostAsync<UserDTO>("user/password", item);

    /// <summary>Deletes an account and its data.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>A task that completes on success.</returns>
    public Task UserDeleteAsync(string identifier) => DeleteAsync($"user?identifier={Uri.EscapeDataString(identifier)}");

    // ----- Portfolio -----

    /// <summary>The Overview: value, chart, allocation, movers and income.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="range">The chart range.</param>
    /// <returns>The overview.</returns>
    public Task<OverviewDTO> PortfolioOverviewAsync(string userId, ChartRange range) =>
        GetAsync<OverviewDTO>($"portfolio/overview?userId={Uri.EscapeDataString(userId)}&range={range}");

    /// <summary>The open positions.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The holdings.</returns>
    public Task<HoldingsDTO> PortfolioHoldingsAsync(string userId) => GetAsync<HoldingsDTO>($"portfolio/holdings?userId={Uri.EscapeDataString(userId)}");

    /// <summary>One position, with its chart and activity.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="key">The instrument key.</param>
    /// <param name="range">The chart range.</param>
    /// <returns>The position.</returns>
    public Task<PositionDetailDTO> PortfolioPositionAsync(string userId, string key, ChartRange range) =>
        GetAsync<PositionDetailDTO>($"portfolio/position?userId={Uri.EscapeDataString(userId)}&key={Uri.EscapeDataString(key)}&range={range}");

    /// <summary>A page of the activity.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="kind">Only one kind (null: everything).</param>
    /// <param name="skip">Lines to skip.</param>
    /// <param name="take">Lines to return.</param>
    /// <returns>The page.</returns>
    public Task<ActivityPageDTO> PortfolioActivityAsync(string userId, ActivityKind? kind, int skip, int take) =>
        GetAsync<ActivityPageDTO>($"portfolio/activity?userId={Uri.EscapeDataString(userId)}{(kind is { } only ? "&kind=" + only : string.Empty)}&skip={skip}&take={take}");

    /// <summary>The Income screen of a year.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="year">The year (this one when null).</param>
    /// <returns>The income.</returns>
    public Task<IncomeDTO> PortfolioIncomeAsync(string userId, int? year) =>
        GetAsync<IncomeDTO>($"portfolio/income?userId={Uri.EscapeDataString(userId)}{(year is { } value ? "&year=" + value.ToString(CultureInfo.InvariantCulture) : string.Empty)}");

    // ----- Insights -----

    /// <summary>Money- and time-weighted returns, against MSCI World and the S&amp;P 500.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="range">The range.</param>
    /// <returns>The performance.</returns>
    public Task<PerformanceDTO> InsightsPerformanceAsync(string userId, ChartRange range) =>
        GetAsync<PerformanceDTO>($"insights/performance?userId={Uri.EscapeDataString(userId)}&range={range}");

    /// <summary>Currencies, sectors, countries and concentration.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The exposure.</returns>
    public Task<ExposureDTO> InsightsExposureAsync(string userId) => GetAsync<ExposureDTO>($"insights/exposure?userId={Uri.EscapeDataString(userId)}");

    /// <summary>What the sales of a year made.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="year">The year (this one when null).</param>
    /// <returns>The realized gains.</returns>
    public Task<RealizedDTO> InsightsRealizedAsync(string userId, int? year) =>
        GetAsync<RealizedDTO>($"insights/realized?userId={Uri.EscapeDataString(userId)}{(year is { } value ? "&year=" + value.ToString(CultureInfo.InvariantCulture) : string.Empty)}");

    /// <summary>Where the portfolio is now, for the projection.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The goals' starting point.</returns>
    public Task<GoalsDTO> InsightsGoalsAsync(string userId) => GetAsync<GoalsDTO>($"insights/goals?userId={Uri.EscapeDataString(userId)}");

    /// <summary>The AI coach and its last review.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The coach.</returns>
    public Task<CoachDTO> InsightsCoachAsync(string userId) => GetAsync<CoachDTO>($"insights/coach?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Starts a new review (poll <see cref="InsightsCoachAsync"/>).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The coach, running.</returns>
    public Task<CoachDTO> InsightsCoachAnalyseAsync(string userId) => PostAsync<CoachDTO>($"insights/coach?userId={Uri.EscapeDataString(userId)}", null);

        // ----- Connection -----

    /// <summary>The Trading 212 connection, its state and the last runs.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    public Task<ConnectionDTO> ConnectionGetAsync(string userId) => GetAsync<ConnectionDTO>($"connection?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Checks a key and secret with Trading 212, saves them and starts the first sync.</summary>
    /// <param name="item">The account, key, secret and kind of account.</param>
    /// <returns>The connection.</returns>
    public Task<ConnectionDTO> ConnectionConnectAsync(ConnectRequest item) => PostAsync<ConnectionDTO>("connection", item);

    /// <summary>Saves what the sync reads.</summary>
    /// <param name="item">The choices.</param>
    /// <returns>The connection.</returns>
    public Task<ConnectionDTO> ConnectionOptionsAsync(ConnectionOptionsRequest item) => PostAsync<ConnectionDTO>("connection/options", item);

    /// <summary>Starts a sync now.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    public Task<ConnectionDTO> ConnectionSyncAsync(string userId) => PostAsync<ConnectionDTO>($"connection/sync?userId={Uri.EscapeDataString(userId)}", null);

    /// <summary>Forgets the key and secret.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    public Task<ConnectionDTO> ConnectionDeleteAsync(string userId) => DeleteAsync<ConnectionDTO>($"connection?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Deletes everything imported.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    public Task<ConnectionDTO> ConnectionDeleteDataAsync(string userId) => DeleteAsync<ConnectionDTO>($"connection/data?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Imports a Trading 212 CSV export.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <returns>What was added.</returns>
    public async Task<CsvImportResultDTO> ConnectionCsvAsync(string userId, Stream file, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"connection/csv?userId={Uri.EscapeDataString(userId)}") { Content = new StreamContent(file) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadAsync<CsvImportResultDTO>(response);
    }

    // ----- Logo -----

    /// <summary>The logo of an instrument, or null when there is none.</summary>
    /// <param name="key">The instrument key.</param>
    /// <param name="symbol">The symbol, for instruments without an ISIN.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>(content type, bytes), or null.</returns>
    public async Task<(string ContentType, byte[] Data)?> LogoGetAsync(string key, string? symbol, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"logo?key={Uri.EscapeDataString(key)}{(string.IsNullOrEmpty(symbol) ? string.Empty : "&symbol=" + Uri.EscapeDataString(symbol))}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return (response.Content.Headers.ContentType?.MediaType ?? "image/png", await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    // ----- Health -----

    /// <summary>Checks the WebApi and its database.</summary>
    /// <returns>The health.</returns>
    public Task<HealthDTO> HealthGetAsync() => GetAsync<HealthDTO>("health");

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await httpClient.GetAsync(path);
        return await ReadAsync<T>(response);
    }

    private async Task<T> PostAsync<T>(string path, object? body)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, SerializerOptions);
        return await ReadAsync<T>(response);
    }

    private async Task DeleteAsync(string path)
    {
        using var response = await httpClient.DeleteAsync(path);
        await EnsureSuccessAsync(response);
    }

    private async Task<T> DeleteAsync<T>(string path)
    {
        using var response = await httpClient.DeleteAsync(path);
        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(SerializerOptions))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiValidationProblemDetails? problems = null;
        try
        {
            problems = await response.Content.ReadFromJsonAsync<ApiValidationProblemDetails>(SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Not a problem body (an empty 401, an HTML error page).
        }

        throw new ObolApiException(response.StatusCode, problems);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
