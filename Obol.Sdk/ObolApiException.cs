using System.Net;
using Obol.Library.Validation;

namespace Obol.Sdk;

/// <summary>
/// Thrown when the WebApi answers with an error. Carries the validation problems when the API sent them.
/// </summary>
/// <param name="statusCode">The HTTP status code.</param>
/// <param name="problems">The validation problems, when present.</param>
public sealed class ObolApiException(HttpStatusCode statusCode, ApiValidationProblemDetails? problems)
    : Exception(problems?.Title is { Length: > 0 } title ? title : $"Obol API returned {(int)statusCode} ({statusCode}).")
{
    /// <summary>Gets the HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>Gets the validation problems, when present.</summary>
    public ApiValidationProblemDetails? Problems { get; } = problems;

    /// <summary>
    /// Flattens the problems into user-facing messages.
    /// </summary>
    /// <returns>One message per error, or the title.</returns>
    public IReadOnlyList<ApiValidationError> ToValidationErrors() =>
        Problems is { Errors.Count: > 0 }
            ? Problems.Errors
            : [new ApiValidationError(string.Empty, Message)];
}
