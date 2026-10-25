using Obol.WebApi.Features.Users.Models.Entities;

namespace Obol.WebApi.Features.Users.Repositories;

/// <summary>
/// The accounts (dbo.User).
/// </summary>
internal interface IUserRepository
{
    /// <summary>Counts the accounts.</summary>
    /// <returns>How many exist.</returns>
    Task<int> CountAsync();

    /// <summary>Lists every account.</summary>
    /// <returns>The accounts.</returns>
    Task<List<UserAccount>> ListAsync();

    /// <summary>Gets an account by its public id.</summary>
    /// <param name="userId">The id.</param>
    /// <returns>The account, or null.</returns>
    Task<UserAccount?> GetAsync(Guid userId);

    /// <summary>Gets an account by user name (ignoring case).</summary>
    /// <param name="userName">The user name.</param>
    /// <returns>The account, or null.</returns>
    Task<UserAccount?> GetByNameAsync(string userName);

    /// <summary>Creates an account; the first one becomes the admin.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="passwordHash">The password hash.</param>
    /// <param name="securityStamp">A fresh stamp.</param>
    /// <returns>The account, or null when the name is taken.</returns>
    Task<UserAccount?> CreateAsync(string userName, string passwordHash, string securityStamp);

    /// <summary>Replaces the password hash and the stamp.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="passwordHash">The new hash.</param>
    /// <param name="securityStamp">The new stamp.</param>
    /// <returns>A task.</returns>
    Task SetPasswordAsync(Guid userId, string passwordHash, string securityStamp);

    /// <summary>Records a successful sign-in.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task TouchLoginAsync(Guid userId);

    /// <summary>Deletes an account and everything it owns.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid userId);
}
