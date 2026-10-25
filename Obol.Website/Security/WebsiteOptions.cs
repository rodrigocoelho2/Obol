namespace Obol.Website.Security;

/// <summary>
/// Sign-in settings, bound from the "Website" section (in Docker: Website__SetupKey, Website__AllowRegistration,
/// Website__KeysDirectory).
/// </summary>
public sealed class WebsiteOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Website";

    /// <summary>
    /// Gets or sets the setup key (OBOL_SETUP_KEY): needed to create the FIRST account, so nobody else on the network
    /// can claim an empty install. Empty lets anyone who reaches the page create the first account.
    /// </summary>
    public string SetupKey { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether anyone may create an account after the first one.</summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets where the cookie encryption keys are kept. Empty keeps them in memory, so every restart signs everyone out.
    /// </summary>
    public string KeysDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets how long "Keep me signed in" keeps a device signed in.</summary>
    public int RememberDays { get; set; } = 30;

    /// <summary>Gets or sets the time zone the days are counted in; keep it the same as the WebApi's Obol:TimeZone.</summary>
    public string TimeZone { get; set; } = "Europe/Lisbon";

    /// <summary>Gets a value indicating whether the first account needs the setup key.</summary>
    public bool RequiresSetupKey => !string.IsNullOrEmpty(SetupKey);
}
