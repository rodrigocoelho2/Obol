namespace Obol.Sdk.Models;

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
