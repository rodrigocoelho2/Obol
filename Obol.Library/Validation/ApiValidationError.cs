namespace Obol.Library.Validation;

/// <summary>
/// One validation problem, tied to the field that caused it.
/// </summary>
/// <param name="Field">The field identifier (e.g. "BirthDate").</param>
/// <param name="Message">The translated, user-facing message.</param>
public sealed record ApiValidationError(string Field, string Message);
