using Obol.WebApi.Extensions.Security;

namespace Obol.UnitTest.Security;

/// <summary>
/// The Trading 212 key and secret are kept encrypted (AES-GCM) with a key derived from OBOL_ENCRYPTION_KEY.
/// </summary>
public sealed class SecretProtectorTests
{
    [Fact]
    public void ASecretComesBackAsItWent()
    {
        var protector = new SecretProtector(TestSupport.Options());

        var cipher = protector.Protect("my-trading-212-secret");

        Assert.StartsWith("v1.", cipher, StringComparison.Ordinal);
        Assert.DoesNotContain("trading", cipher, StringComparison.Ordinal);
        Assert.Equal("my-trading-212-secret", protector.Unprotect(cipher));
    }

    [Fact]
    public void TheSameSecretIsNeverEncryptedTheSameWayTwice()
    {
        var protector = new SecretProtector(TestSupport.Options());

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void AnotherKeyOrATamperedCipherReadsNothing()
    {
        var protector = new SecretProtector(TestSupport.Options());
        var other = new SecretProtector(TestSupport.Options(configure: static options => options.EncryptionKey = "a-different-key-0123456789"));
        var cipher = protector.Protect("secret");
        var parts = cipher.Split('.');
        var bytes = Convert.FromBase64String(parts[2]);
        bytes[0] ^= 0xFF;
        var tampered = string.Join('.', parts[0], parts[1], Convert.ToBase64String(bytes), parts[3]);

        Assert.Null(other.Unprotect(cipher));
        Assert.Null(protector.Unprotect(tampered));
        Assert.Null(protector.Unprotect("plain-text"));
        Assert.Null(protector.Unprotect(string.Empty));
    }
}
