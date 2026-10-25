using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Obol.WebApi.Features.Users.Services;

/// <summary>
/// PBKDF2-SHA256 password hashes: "pbkdf2-sha256.{iterations}.{salt}.{hash}" (base64), compared in constant time.
/// </summary>
internal static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>
    /// Hashes a password with a new random salt.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns>The hash string.</returns>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return string.Join('.', Prefix, Iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Checks a password against a hash.
    /// </summary>
    /// <param name="password">The password typed.</param>
    /// <param name="stored">The stored hash.</param>
    /// <returns>True when they match.</returns>
    public static bool Verify(string password, string stored)
    {
        var parts = (stored ?? string.Empty).Split('.');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations < 1)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password ?? string.Empty), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
