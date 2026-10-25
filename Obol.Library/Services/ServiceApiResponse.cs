using Obol.Library.Validation;

namespace Obol.Library.Services;

/// <summary>
/// The outcome kind of a service call; the WebApi maps it to an HTTP status code.
/// </summary>
public enum ServiceApiResponseStatus
{
    /// <summary>200 OK.</summary>
    Success,

    /// <summary>400 Bad Request with validation problems.</summary>
    ValidationProblems,

    /// <summary>404 Not Found.</summary>
    NotFound,

    /// <summary>401 Unauthorized.</summary>
    Unauthorized,
}

/// <summary>
/// The result of every service method: either a value, or validation problems. Services never throw for
/// expected failures and never pick an HTTP status code themselves.
/// </summary>
/// <typeparam name="T">The payload type.</typeparam>
public sealed class ServiceApiResponse<T>
{
    private ServiceApiResponse(ServiceApiResponseStatus status, T? result, ApiValidationProblemDetails? problems)
    {
        Status = status;
        Result = result;
        Problems = problems;
    }

    /// <summary>Gets the outcome kind.</summary>
    public ServiceApiResponseStatus Status { get; }

    /// <summary>Gets the payload (only on success).</summary>
    public T? Result { get; }

    /// <summary>Gets the problems (only on failure).</summary>
    public ApiValidationProblemDetails? Problems { get; }

    /// <summary>Gets a value indicating whether the call succeeded.</summary>
    public bool IsSuccess => Status == ServiceApiResponseStatus.Success;

    /// <summary>
    /// A successful response carrying a payload.
    /// </summary>
    /// <param name="result">The payload.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Success(T result) => new(ServiceApiResponseStatus.Success, result, null);

    /// <summary>
    /// A validation failure (HTTP 400).
    /// </summary>
    /// <param name="title">The translated summary.</param>
    /// <returns>The response; add details with <see cref="AddError"/> / <see cref="AddErrorRange"/>.</returns>
    public static ServiceApiResponse<T> ValidationProblems(string title) =>
        new(ServiceApiResponseStatus.ValidationProblems, default, new ApiValidationProblemDetails { Title = title, Status = 400 });

    /// <summary>
    /// A missing record (HTTP 404).
    /// </summary>
    /// <param name="title">The translated summary.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> NotFound(string title) =>
        new(ServiceApiResponseStatus.NotFound, default, new ApiValidationProblemDetails { Title = title, Status = 404 });

    /// <summary>
    /// Not allowed (HTTP 401).
    /// </summary>
    /// <param name="title">The translated summary.</param>
    /// <returns>The response.</returns>
    public static ServiceApiResponse<T> Unauthorized(string title) =>
        new(ServiceApiResponseStatus.Unauthorized, default, new ApiValidationProblemDetails { Title = title, Status = 401 });

    /// <summary>
    /// Adds one error to a failure response.
    /// </summary>
    /// <param name="field">The field identifier.</param>
    /// <param name="message">The translated message.</param>
    /// <returns>This response, for chaining.</returns>
    public ServiceApiResponse<T> AddError(string field, string message)
    {
        Problems?.Errors.Add(new ApiValidationError(field, message));
        return this;
    }

    /// <summary>
    /// Adds several errors to a failure response.
    /// </summary>
    /// <param name="errors">The errors to add.</param>
    /// <returns>This response, for chaining.</returns>
    public ServiceApiResponse<T> AddErrorRange(IEnumerable<ApiValidationError> errors)
    {
        Problems?.Errors.AddRange(errors);
        return this;
    }
}
