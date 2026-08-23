using System.Security.Cryptography;
using System.Text;

namespace DataGateMonitor.Services.Api.Auth;

/// <summary>
/// Hashing and verification for <see cref="Models.ClientApplication.ClientSecret"/>.
/// New secrets are stored as bcrypt; legacy plaintext rows are accepted and upgraded on successful auth.
/// </summary>
public static class ClientApplicationSecret
{
    public static bool LooksLikeBcrypt(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || stored.Length < 4)
            return false;

        return stored.StartsWith("$2a$", StringComparison.Ordinal)
               || stored.StartsWith("$2b$", StringComparison.Ordinal)
               || stored.StartsWith("$2y$", StringComparison.Ordinal);
    }

    public static string Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return BCrypt.Net.BCrypt.HashPassword(plaintext);
    }

    public static bool Verify(string? plaintext, string? stored)
    {
        if (string.IsNullOrEmpty(plaintext) || string.IsNullOrEmpty(stored))
            return false;

        if (LooksLikeBcrypt(stored))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(plaintext, stored);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return FixedTimeEqualsUtf8(stored, plaintext);
    }

    public static bool FixedTimeEqualsUtf8(string? stored, string? provided)
    {
        var storedHash = SHA256.HashData(Encoding.UTF8.GetBytes(stored ?? string.Empty));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(storedHash, providedHash);
    }
}
