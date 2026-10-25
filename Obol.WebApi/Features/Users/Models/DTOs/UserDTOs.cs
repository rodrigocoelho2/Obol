using Obol.Library.Enums;

namespace Obol.WebApi.Features.Users.Models.DTOs;

/// <summary>An account (never the password hash).</summary>
public sealed class UserDTO
{
    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the user name.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the role.</summary>
    public UserRole Role { get; set; }

    /// <summary>Gets or sets when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the last successful sign-in.</summary>
    public DateTimeOffset? LastSignInAt { get; set; }

    /// <summary>Gets or sets the security stamp (changes with the password; ends the other sessions).</summary>
    public string SecurityStamp { get; set; } = string.Empty;
}

/// <summary>Whether any account exists (the first visit creates the first one), and the demo account when there is one.</summary>
public sealed class UserStatusDTO
{
    /// <summary>Gets or sets how many accounts exist.</summary>
    public int UserCount { get; set; }

    /// <summary>Gets or sets a value indicating whether Obol runs on sample data (demo mode).</summary>
    public bool DemoMode { get; set; }

    /// <summary>Gets or sets the demo account's user name (demo mode only).</summary>
    public string? DemoUserName { get; set; }

    /// <summary>Gets or sets the demo account's password (demo mode only; it's sample data on purpose).</summary>
    public string? DemoPassword { get; set; }
}

/// <summary>Creates an account.</summary>
public sealed class UserCreateRequest
{
    /// <summary>Gets or sets the user name.</summary>
    public string? UserName { get; set; }

    /// <summary>Gets or sets the password.</summary>
    public string? Password { get; set; }
}

/// <summary>Checks a user name and password.</summary>
public sealed class UserSignInRequest
{
    /// <summary>Gets or sets the user name.</summary>
    public string? UserName { get; set; }

    /// <summary>Gets or sets the password.</summary>
    public string? Password { get; set; }
}

/// <summary>Changes the password of an account.</summary>
public sealed class UserPasswordRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the current password.</summary>
    public string? CurrentPassword { get; set; }

    /// <summary>Gets or sets the new password.</summary>
    public string? NewPassword { get; set; }
}
