namespace Obol.WebApi.Features.Users.Models.Entities;

/// <summary>
/// An account (dbo.User). The password is only kept as a salted PBKDF2 hash.
/// </summary>
public sealed class UserAccount
{
    /// <summary>Gets or sets the internal key (never leaves the WebApi).</summary>
    public long UserID { get; set; }

    /// <summary>Gets or sets the public identifier (the Website's cookie holds it).</summary>
    public Guid UserUniqueID { get; set; }

    /// <summary>Gets or sets the user name (unique, ignoring case).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the password hash (<see cref="Services.PasswordHasher"/> format).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the account is an admin (the first account is).</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Gets or sets a random value that changes with the password, so other sessions end.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>Gets or sets when the account was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the last successful sign-in (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }
}
