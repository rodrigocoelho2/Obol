using Microsoft.AspNetCore.Mvc;
using Obol.Library.Validation;
using Obol.Sdk;
using Obol.Sdk.Models;
using Obol.Website.Model;

namespace Obol.Website.Controllers;

/// <summary>
/// The Sync screen: connect Trading 212, what to sync, sync now, import a CSV export, start over. The actions answer
/// JSON (<see cref="AjaxResult"/>) to the page script.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="logger">Logger (never logs a key).</param>
public sealed class SyncController(ObolClient apiClient, ILogger<SyncController> logger) : WebsiteBaseController(apiClient)
{
    /// <summary>The Sync screen.</summary>
    /// <returns>The view.</returns>
    [HttpGet("/sync")]
    public async Task<IActionResult> Index() => View("Index", await Screens.SyncAsync(Shell(new SyncPageModel(), AppSection.Sync), UserId));

    /// <summary>The connection, for the page to follow a running sync.</summary>
    /// <returns>The connection.</returns>
    [HttpGet("/sync/status")]
    public async Task<IActionResult> Status() => Json(await Call(() => ApiClient.ConnectionGetAsync(UserId)));

    /// <summary>Checks and saves a Trading 212 key and secret, then starts the first sync.</summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="apiSecret">The API secret.</param>
    /// <param name="environment">live or demo.</param>
    /// <returns>The connection, or the problems.</returns>
    [HttpPost("/sync/connect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect([FromForm] string? apiKey, [FromForm] string? apiSecret, [FromForm] string? environment)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Connecting Trading 212 ({Environment}) for {User}", environment, User.Identity?.Name);

        return Json(await Call(() => ApiClient.ConnectionConnectAsync(new ConnectRequest
        {
            UserId = UserId,
            ApiKey = apiKey,
            ApiSecret = apiSecret,
            Environment = environment,
        })));
    }

    /// <summary>Saves what the sync reads.</summary>
    /// <param name="item">The choices.</param>
    /// <returns>The connection.</returns>
    [HttpPost("/sync/options")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Options([FromBody] ConnectionOptionsRequest item)
    {
        item.UserId = UserId;
        return Json(await Call(() => ApiClient.ConnectionOptionsAsync(item)));
    }

    /// <summary>Starts a sync now.</summary>
    /// <returns>The connection.</returns>
    [HttpPost("/sync/now")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Now() => Json(await Call(() => ApiClient.ConnectionSyncAsync(UserId)));

    /// <summary>Forgets the key and secret.</summary>
    /// <returns>The connection.</returns>
    [HttpPost("/sync/disconnect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect() => Json(await Call(() => ApiClient.ConnectionDeleteAsync(UserId)));

    /// <summary>Deletes everything imported.</summary>
    /// <returns>The connection.</returns>
    [HttpPost("/sync/delete-data")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteData() => Json(await Call(() => ApiClient.ConnectionDeleteDataAsync(UserId)));

    /// <summary>Imports a Trading 212 CSV export.</summary>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <returns>What was added, or the problems.</returns>
    [HttpPost("/sync/csv")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 32 * 1024 * 1024)]
    public async Task<IActionResult> Csv(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Json(new AjaxDataResult<CsvImportResultDTO> { IsValid = false, ValidationErrors = [new ApiValidationError("File", "Pick the CSV file exported from Trading 212.")] });
        }

        await using var stream = file.OpenReadStream();
        return Json(await Call(() => ApiClient.ConnectionCsvAsync(UserId, stream, cancellationToken)));
    }

    private static async Task<AjaxDataResult<T>> Call<T>(Func<Task<T>> call)
    {
        try
        {
            return new AjaxDataResult<T> { Data = await call() };
        }
        catch (ObolApiException exception)
        {
            return new AjaxDataResult<T> { IsValid = false, ValidationErrors = [.. exception.ToValidationErrors()] };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new AjaxDataResult<T> { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, WebsiteBaseModel.ApiUnavailableMessage)] };
        }
    }
}
