using Obol.Library.Services;
using Microsoft.AspNetCore.Mvc;

namespace Obol.WebApi.Extensions.Services;

/// <summary>
/// Maps a <see cref="ServiceApiResponse{T}"/> to the HTTP result. Controllers never pick status codes themselves.
/// </summary>
internal static class ServiceApiResponseExtensions
{
    /// <summary>
    /// Converts a service response into an action result.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="response">The service response.</param>
    /// <returns>200 with the payload, 400 with validation problems, 401 or 404.</returns>
    public static ActionResult ToActionResult<T>(this ServiceApiResponse<T> response) => response.Status switch
    {
        ServiceApiResponseStatus.Success when response.Result is null => new OkResult(),
        ServiceApiResponseStatus.Success => new OkObjectResult(response.Result),
        ServiceApiResponseStatus.NotFound => new NotFoundObjectResult(response.Problems),
        ServiceApiResponseStatus.Unauthorized => new UnauthorizedObjectResult(response.Problems),
        _ => new BadRequestObjectResult(response.Problems),
    };
}
