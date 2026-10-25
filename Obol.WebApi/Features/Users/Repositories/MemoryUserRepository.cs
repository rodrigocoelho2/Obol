using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Features.Users.Models.Entities;

namespace Obol.WebApi.Features.Users.Repositories;

/// <summary>
/// The accounts in demo mode (<see cref="MemoryStore"/>): same behaviour as the stored procedures.
/// </summary>
/// <param name="store">The in-memory tables.</param>
/// <param name="timeProvider">The clock.</param>
internal sealed class MemoryUserRepository(MemoryStore store, TimeProvider timeProvider) : IUserRepository
{
    private long _nextId;

    /// <inheritdoc />
    public Task<int> CountAsync()
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Users.Count);
        }
    }

    /// <inheritdoc />
    public Task<List<UserAccount>> ListAsync()
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Users.Select(Copy).ToList());
        }
    }

    /// <inheritdoc />
    public Task<UserAccount?> GetAsync(Guid userId)
    {
        lock (store.Gate)
        {
            var user = store.Users.FirstOrDefault(user => user.UserUniqueID == userId);
            return Task.FromResult(user is null ? null : Copy(user));
        }
    }

    /// <inheritdoc />
    public Task<UserAccount?> GetByNameAsync(string userName)
    {
        lock (store.Gate)
        {
            var user = store.Users.FirstOrDefault(user => user.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user is null ? null : Copy(user));
        }
    }

    /// <inheritdoc />
    public Task<UserAccount?> CreateAsync(string userName, string passwordHash, string securityStamp)
    {
        lock (store.Gate)
        {
            if (store.Users.Any(user => user.Username.Equals(userName, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult<UserAccount?>(null);
            }

            var user = new UserAccount
            {
                UserID = Interlocked.Increment(ref _nextId),
                UserUniqueID = Guid.NewGuid(),
                Username = userName,
                PasswordHash = passwordHash,
                IsAdmin = store.Users.Count == 0,
                SecurityStamp = securityStamp,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            };
            store.Users.Add(user);
            return Task.FromResult<UserAccount?>(Copy(user));
        }
    }

    /// <inheritdoc />
    public Task SetPasswordAsync(Guid userId, string passwordHash, string securityStamp)
    {
        lock (store.Gate)
        {
            if (store.Users.FirstOrDefault(user => user.UserUniqueID == userId) is { } user)
            {
                user.PasswordHash = passwordHash;
                user.SecurityStamp = securityStamp;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TouchLoginAsync(Guid userId)
    {
        lock (store.Gate)
        {
            if (store.Users.FirstOrDefault(user => user.UserUniqueID == userId) is { } user)
            {
                user.LastLoginAt = timeProvider.GetUtcNow().UtcDateTime;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId)
    {
        lock (store.Gate)
        {
            store.Users.RemoveAll(user => user.UserUniqueID == userId);
            store.Connections.Remove(userId);
            store.Runs.Remove(userId);
            store.Portfolios.Remove(userId);
        }

        return Task.CompletedTask;
    }

    private static UserAccount Copy(UserAccount user) => new()
    {
        UserID = user.UserID,
        UserUniqueID = user.UserUniqueID,
        Username = user.Username,
        PasswordHash = user.PasswordHash,
        IsAdmin = user.IsAdmin,
        SecurityStamp = user.SecurityStamp,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt,
    };
}
