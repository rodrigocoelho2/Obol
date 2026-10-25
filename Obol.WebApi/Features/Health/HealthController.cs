using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Obol.WebApi.Extensions.Mvc;
using Obol.WebApi.Features.Users.Repositories;
using Swashbuckle.AspNetCore.Annotations;

namespace Obol.WebApi.Features.Health;

/// <summary>Whether the WebApi and its database answer.</summary>
public sealed class HealthDTO
{
    /// <summary>Gets or sets a value indicating whether everything works.</summary>
    public bool Ok { get; set; }

    /// <summary>Gets or sets a value indicating whether Obol runs on sample data.</summary>
    public bool DemoMode { get; set; }

    /// <summary>Gets or sets what went wrong, when something did.</summary>
    public string? Problem { get; set; }
}

/// <summary>Health check for the Website and Docker.</summary>
/// <param name="userRepository">Accounts (a cheap database call).</param>
/// <param name="options">Module options.</param>
[Authorize]
[Route("health")]
[ApiController]
internal sealed class HealthController(IUserRepository userRepository, IOptions<ObolOptions> options) : InternalControllerBase
{
    /// <summary>Checks the WebApi and its database.</summary>
    /// <returns>The health.</returns>
    [HttpGet]
    [ProducesResponseType<HealthDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "HealthGet", OperationId = "HealthGet")]
    public async Task<ActionResult<HealthDTO>> HealthGetAsync()
    {
        try
        {
            await userRepository.CountAsync();
            return Ok(new HealthDTO { Ok = true, DemoMode = options.Value.DemoMode });
        }
        catch (Exception exception) when (exception is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            return Ok(new HealthDTO { Ok = false, DemoMode = options.Value.DemoMode, Problem = "The database doesn't answer: " + exception.Message });
        }
    }
}
