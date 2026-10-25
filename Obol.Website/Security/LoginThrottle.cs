using System.Collections.Concurrent;

namespace Obol.Website.Security;

/// <summary>
/// Slows down password guessing: after <see cref="MaxFailures"/> wrong passwords from the same address within
/// <see cref="Window"/>, that address has to wait <see cref="LockTime"/> before trying again.
/// </summary>
/// <param name="timeProvider">The clock.</param>
public sealed class LoginThrottle(TimeProvider timeProvider)
{
    /// <summary>Wrong passwords allowed inside the window.</summary>
    public const int MaxFailures = 5;

    /// <summary>The window failures are counted in.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>How long an address waits after too many failures.</summary>
    public static readonly TimeSpan LockTime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Attempts> _attempts = new(StringComparer.Ordinal);

    /// <summary>
    /// How long the address still has to wait, or <see cref="TimeSpan.Zero"/> when it may try.
    /// </summary>
    /// <param name="address">The client address.</param>
    /// <returns>The remaining wait.</returns>
    public TimeSpan RetryAfter(string address)
    {
        var now = timeProvider.GetUtcNow();
        return _attempts.TryGetValue(address, out var attempts) && attempts.LockedUntil is { } until && until > now ? until - now : TimeSpan.Zero;
    }

    /// <summary>
    /// Records a wrong password.
    /// </summary>
    /// <param name="address">The client address.</param>
    public void Failed(string address)
    {
        var now = timeProvider.GetUtcNow();
        _attempts.AddOrUpdate(
            address,
            _ => new Attempts(1, now, null),
            (_, attempts) =>
            {
                var expired = now - attempts.FirstAt > Window || (attempts.LockedUntil is { } until && until <= now);
                var current = expired ? new Attempts(0, now, null) : attempts;
                var failures = current.Failures + 1;
                return current with { Failures = failures, LockedUntil = failures >= MaxFailures ? now + LockTime : null };
            });

        if (_attempts.Count > 10_000)
        {
            foreach (var (key, value) in _attempts)
            {
                if (now - value.FirstAt > Window && (value.LockedUntil is null || value.LockedUntil <= now))
                {
                    _attempts.TryRemove(key, out _);
                }
            }
        }
    }

    /// <summary>
    /// Forgets the failures of an address after a correct password.
    /// </summary>
    /// <param name="address">The client address.</param>
    public void Succeeded(string address) => _attempts.TryRemove(address, out _);

    private sealed record Attempts(int Failures, DateTimeOffset FirstAt, DateTimeOffset? LockedUntil);
}
