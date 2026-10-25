using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Obol.WebApi.Extensions.Mvc;
using Obol.WebApi.Features.Logos.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Obol.WebApi.Features.Logos.Controllers;

/// <summary>Company and fund logos (only when a logo.dev key is set).</summary>
/// <param name="logoService">The logos.</param>
[Authorize]
[Route("logo")]
[ApiController]
internal sealed class LogoController(LogoService logoService) : InternalControllerBase
{
    /// <summary>The logo of an instrument, as an image; 404 when there is none.</summary>
    /// <param name="key">The instrument key (ISIN or T212:ticker).</param>
    /// <param name="symbol">The symbol, for instruments without an ISIN.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>The image.</returns>
    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "LogoGet", OperationId = "LogoGet")]
    public async Task<IActionResult> LogoGetAsync(string key, string? symbol, CancellationToken cancellationToken)
    {
        var logo = await logoService.GetAsync(key, symbol, cancellationToken);
        return logo is null ? NotFound() : File(logo.Data, logo.ContentType);
    }
}
