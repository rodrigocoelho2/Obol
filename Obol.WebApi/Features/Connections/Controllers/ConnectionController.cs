using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Obol.Library.Validation;
using Obol.WebApi.Extensions.Mvc;
using Obol.WebApi.Extensions.Services;
using Obol.WebApi.Features.Connections.Models.DTOs;
using Obol.WebApi.Features.Connections.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Obol.WebApi.Features.Connections.Controllers;

/// <summary>The Trading 212 connection, the sync and CSV imports. Keys never appear in logs.</summary>
/// <param name="connectionService">The connection service.</param>
/// <param name="logger">Logger.</param>
[Authorize]
[Route("connection")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class ConnectionController(IConnectionService connectionService, ILogger<ConnectionController> logger) : InternalControllerBase
{
    /// <summary>The connection, its state and the last runs.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    [HttpGet]
    [ProducesResponseType<ConnectionDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ConnectionGet", OperationId = "ConnectionGet")]
    public async Task<ActionResult<ConnectionDTO>> ConnectionGetAsync(string userId) => (await connectionService.GetAsync(userId)).ToActionResult();

    /// <summary>Checks a key and secret with Trading 212, saves them and starts the first sync.</summary>
    /// <param name="item">The account, key, secret and kind of account.</param>
    /// <returns>The connection.</returns>
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ConnectionDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ConnectionConnect", OperationId = "ConnectionConnect")]
    public async Task<ActionResult<ConnectionDTO>> ConnectionConnectAsync(ConnectRequest item)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("ConnectionConnect [UserId={UserId}, Environment={Environment}]", item.UserId, item.Environment);

        return (await connectionService.ConnectAsync(item)).ToActionResult();
    }

    /// <summary>Saves what the sync reads.</summary>
    /// <param name="item">The choices.</param>
    /// <returns>The connection.</returns>
    [HttpPost("options")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<ConnectionDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ConnectionOptions", OperationId = "ConnectionOptions")]
    public async Task<ActionResult<ConnectionDTO>> ConnectionOptionsAsync(ConnectionOptionsRequest item) =>
        (await connectionService.SetOptionsAsync(item)).ToActionResult();

    /// <summary>Starts a sync now (it runs in the background).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    [HttpPost("sync")]
    [ProducesResponseType<ConnectionDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ConnectionSync", OperationId = "ConnectionSync")]
    public async Task<ActionResult<ConnectionDTO>> ConnectionSyncAsync(string userId) => (await connectionService.SyncNowAsync(userId)).ToActionResult();

    /// <summary>Forgets the key and secret (the portfolio stays).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    [HttpDelete]
    [ProducesResponseType<ConnectionDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ConnectionDelete", OperationId = "ConnectionDelete")]
    public async Task<ActionResult<ConnectionDTO>> ConnectionDeleteAsync(string userId) => (await connectionService.DisconnectAsync(userId)).ToActionResult();

    /// <summary>Deletes everything imported, to start over.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    [HttpDelete("data")]
    [ProducesResponseType<ConnectionDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ConnectionDeleteData", OperationId = "ConnectionDeleteData")]
    public async Task<ActionResult<ConnectionDTO>> ConnectionDeleteDataAsync(string userId)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("ConnectionDeleteData [UserId={UserId}]", userId);

        return (await connectionService.DeleteDataAsync(userId)).ToActionResult();
    }

    /// <summary>Imports a Trading 212 CSV export (the file as the body).</summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <returns>What was added.</returns>
    [HttpPost("csv")]
    [Consumes("text/csv", "text/plain", "application/octet-stream")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [ProducesResponseType<CsvImportResultDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ConnectionCsv", OperationId = "ConnectionCsv")]
    public async Task<ActionResult<CsvImportResultDTO>> ConnectionCsvAsync([FromQuery] string userId, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("ConnectionCsv [UserId={UserId}, Bytes={Bytes}]", userId, Request.ContentLength);

        return (await connectionService.ImportCsvAsync(userId, Request.Body, cancellationToken)).ToActionResult();
    }
}
