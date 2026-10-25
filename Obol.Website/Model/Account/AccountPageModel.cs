namespace Obol.Website.Model.Account;

/// <summary>
/// Model of the sign-in and create-account pages (one card, two tabs).
/// </summary>
public sealed class AccountPageModel
{
    /// <summary>Gets or sets the local address to go back to after signing in.</summary>
    public string ReturnUrl { get; set; } = "/";

    /// <summary>Gets or sets the user name typed (kept when the form comes back with an error).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the session is kept after the browser closes.</summary>
    public bool Remember { get; set; } = true;

    /// <summary>Gets or sets the messages shown above the form.</summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>Gets or sets how many days "Keep me signed in" lasts.</summary>
    public int RememberDays { get; set; } = 30;

    /// <summary>Gets or sets a value indicating whether a new account may be created here.</summary>
    public bool CanCreateAccount { get; set; }

    /// <summary>Gets or sets a value indicating whether no account exists yet (the first one is the admin).</summary>
    public bool IsFirstAccount { get; set; }

    /// <summary>Gets or sets a value indicating whether creating the first account asks for the setup key.</summary>
    public bool NeedsSetupKey { get; set; }

    /// <summary>Gets or sets the shortest password accepted.</summary>
    public int MinPasswordLength { get; set; } = 8;

    /// <summary>Gets or sets a message shown when the WebApi cannot be reached.</summary>
    public string? Unavailable { get; set; }

    /// <summary>Gets or sets the demo account's user name (demo mode only).</summary>
    public string? DemoUserName { get; set; }

    /// <summary>Gets or sets the demo account's password (demo mode only).</summary>
    public string? DemoPassword { get; set; }
}
