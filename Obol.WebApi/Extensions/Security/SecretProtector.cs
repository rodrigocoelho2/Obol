using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Obol.WebApi.Features;

namespace Obol.WebApi.Extensions.Security;

/// <summary>
/// Encrypts the Trading 212 API keys before they reach the database.
/// </summary>
internal interface ISecretProtector
{
    /// <summary>
    /// Encrypts a value.
    /// </summary>
    /// <param name="plain">The value.</param>
    /// <returns>"v1.{nonce}.{cipher}.{tag}" in base64.</returns>
    string Protect(string plain);

    /// <summary>
    /// Decrypts a value made by <see cref="Protect"/>.
    /// </summary>
    /// <param name="cipher">The encrypted value.</param>
    /// <returns>The value, or null when it can't be read (another encryption key, or tampered with).</returns>
    string? Unprotect(string cipher);
}

/// <summary>
/// AES-256-GCM with a key derived (SHA-256) from <see cref="ObolOptions.EncryptionKey"/>. In demo mode with no key set,
/// a random key for this run is used (nothing is saved anyway).
/// </summary>
internal sealed class SecretProtector : ISecretProtector
{
    private const string Version = "v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    /// <summary>
    /// Derives the key.
    /// </summary>
    /// <param name="options">The module options.</param>
    public SecretProtector(IOptions<ObolOptions> options)
    {
        var secret = options.Value.EncryptionKey;
        _key = string.IsNullOrEmpty(secret) ? RandomNumberGenerator.GetBytes(32) : SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    /// <inheritdoc />
    public string Protect(string plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, data, cipher, tag);
        return string.Join('.', Version, Convert.ToBase64String(nonce), Convert.ToBase64String(cipher), Convert.ToBase64String(tag));
    }

    /// <inheritdoc />
    public string? Unprotect(string cipher)
    {
        var parts = (cipher ?? string.Empty).Split('.');
        if (parts.Length != 4 || parts[0] != Version)
        {
            return null;
        }

        try
        {
            var nonce = Convert.FromBase64String(parts[1]);
            var data = Convert.FromBase64String(parts[2]);
            var tag = Convert.FromBase64String(parts[3]);
            var plain = new byte[data.Length];
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, data, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }
}
