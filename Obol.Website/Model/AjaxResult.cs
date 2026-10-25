using Obol.Library.Validation;

namespace Obol.Website.Model;

/// <summary>
/// The answer of every Website JSON action.
/// </summary>
public class AjaxResult
{
    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    public bool IsValid { get; set; } = true;

    /// <summary>Gets or sets the validation errors (empty on success).</summary>
    public List<ApiValidationError> ValidationErrors { get; set; } = [];
}

/// <summary>
/// An <see cref="AjaxResult"/> that also carries data.
/// </summary>
/// <typeparam name="T">The data type.</typeparam>
public sealed class AjaxDataResult<T> : AjaxResult
{
    /// <summary>Gets or sets the data (only on success).</summary>
    public T? Data { get; set; }
}
