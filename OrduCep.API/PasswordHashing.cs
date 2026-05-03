using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace OrduCep.API;

public static class PasswordHashing
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object UserContext = new();

    public static string Hash(string password)
    {
        return Hasher.HashPassword(UserContext, password);
    }

    public static bool Verify(string storedHash, string password, out bool needsRehash)
    {
        needsRehash = false;

        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrEmpty(password))
            return false;

        var result = Hasher.VerifyHashedPassword(UserContext, storedHash, password);
        if (result == PasswordVerificationResult.Success)
            return true;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            needsRehash = true;
            return true;
        }

        if (!IsLegacySha256Hash(storedHash))
            return false;

        var legacyHash = LegacySha256(password);
        var storedBytes = Encoding.ASCII.GetBytes(storedHash.ToUpperInvariant());
        var candidateBytes = Encoding.ASCII.GetBytes(legacyHash);
        var success = CryptographicOperations.FixedTimeEquals(storedBytes, candidateBytes);
        needsRehash = success;
        return success;
    }

    private static bool IsLegacySha256Hash(string value)
    {
        return value.Length == 64 && value.All(Uri.IsHexDigit);
    }

    private static string LegacySha256(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
