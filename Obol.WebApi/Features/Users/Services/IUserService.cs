using Obol.Library.Services;
using Obol.WebApi.Features.Users.Models.DTOs;

namespace Obol.WebApi.Features.Users.Services;

/// <summary>
/// The accounts.
/// </summary>
internal interface IUserService
{
    /// <summary>How many accounts exist, and the demo account in demo mode.</summary>
    /// <returns>The status.</returns>
    Task<ServiceApiResponse<UserStatusDTO>> StatusAsync();

    /// <summary>Lists every account.</summary>
    /// <returns>The accounts.</returns>
    Task<ServiceApiResponse<List<UserDTO>>> ListAsync();

    /// <summary>Gets one account.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>The account, or not found.</returns>
    Task<ServiceApiResponse<UserDTO>> GetAsync(string identifier);

    /// <summary>Creates an account (the first one is an admin).</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account, or validation problems.</returns>
    Task<ServiceApiResponse<UserDTO>> CreateAsync(UserCreateRequest item);

    /// <summary>Checks a user name and password.</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account, or a validation problem (never says which of the two was wrong).</returns>
    Task<ServiceApiResponse<UserDTO>> SignInAsync(UserSignInRequest item);

    /// <summary>Changes a password (and the security stamp, which ends the other sessions).</summary>
    /// <param name="item">The account, current and new password.</param>
    /// <returns>The account, or validation problems.</returns>
    Task<ServiceApiResponse<UserDTO>> ChangePasswordAsync(UserPasswordRequest item);

    /// <summary>Deletes an account and its data (never the last admin).</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>Success, not found or a validation problem.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(string identifier);
}
