using DataGateMonitor.Services.Api.Auth;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api.Auth;

public class ClientApplicationSecretTests
{
    [Theory]
    [InlineData("$2a$11$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ012", true)]
    [InlineData("$2b$10$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ012", true)]
    [InlineData("$2y$12$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ012", true)]
    [InlineData("plain-guid-like-secret", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("$2", false)]
    [InlineData("$2x$11$abcdefgh", false)]
    public void LooksLikeBcrypt_Detects_Known_Prefixes(string? stored, bool expected)
    {
        Assert.Equal(expected, ClientApplicationSecret.LooksLikeBcrypt(stored));
    }

    [Fact]
    public void Hash_Then_Verify_RoundTrips()
    {
        var hash = ClientApplicationSecret.Hash("super-secret");
        Assert.True(ClientApplicationSecret.LooksLikeBcrypt(hash));
        Assert.True(ClientApplicationSecret.Verify("super-secret", hash));
        Assert.False(ClientApplicationSecret.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_Rejects_Empty()
    {
        Assert.Throws<ArgumentException>(() => ClientApplicationSecret.Hash(""));
    }

    [Fact]
    public void Verify_LegacyPlaintext_Uses_FixedTime_Compare()
    {
        Assert.True(ClientApplicationSecret.Verify("abc", "abc"));
        Assert.False(ClientApplicationSecret.Verify("abc", "abd"));
        Assert.False(ClientApplicationSecret.Verify("", "abc"));
        Assert.False(ClientApplicationSecret.Verify("abc", ""));
        Assert.False(ClientApplicationSecret.Verify(null, "abc"));
        Assert.False(ClientApplicationSecret.Verify("abc", null));
    }

    [Fact]
    public void Verify_MalformedBcryptPrefix_ReturnsFalse_NotThrow()
    {
        // Looks like bcrypt but is not a valid hash — must not throw to callers.
        Assert.False(ClientApplicationSecret.Verify("secret", "$2a$11$not-a-valid-bcrypt-hash!!!!!"));
    }

    [Fact]
    public void FixedTimeEqualsUtf8_Handles_Null_And_Mismatch()
    {
        Assert.True(ClientApplicationSecret.FixedTimeEqualsUtf8("x", "x"));
        Assert.False(ClientApplicationSecret.FixedTimeEqualsUtf8("x", "y"));
        Assert.True(ClientApplicationSecret.FixedTimeEqualsUtf8(null, null));
        Assert.False(ClientApplicationSecret.FixedTimeEqualsUtf8(null, "x"));
    }
}
