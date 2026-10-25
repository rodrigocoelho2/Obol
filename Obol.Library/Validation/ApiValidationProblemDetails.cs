namespace Obol.Library.Validation;

/// <summary>
/// The body of every non-success API response: a title plus the individual errors.
/// </summary>
public sealed class ApiValidationProblemDetails
{
    /// <summary>Gets or sets the translated summary of the problem.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTTP status code.</summary>
    public int Status { get; set; }

    /// <summary>Gets or sets the individual validation errors.</summary>
    public List<ApiValidationError> Errors { get; set; } = [];
}
