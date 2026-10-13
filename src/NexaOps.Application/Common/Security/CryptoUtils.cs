using System.Security.Cryptography;
using System.Text;

namespace NexaOps.Application.Common.Security;

public static class CryptoUtils
{
    public static string GenerateSecureToken(int byteLength = 64)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public static string HashToken(string token)
    {
        var inputBytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
