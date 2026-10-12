using System.Security.Cryptography;
using System.Text;

namespace HitchTracker.Web.Infrastructure;

/// <summary>Hasht en vergelijkt API-sleutels, zodat sleutels nooit leesbaar in de database staan.</summary>
public static class ApiKeyHasher
{
    public static string Hash(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    /// <summary>Vergelijkt in constante tijd, zodat de sleutel niet via timing te raden is.</summary>
    public static bool Matches(string? apiKey, string expectedHash)
    {
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(expectedHash))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(apiKey)),
            Encoding.ASCII.GetBytes(expectedHash));
    }
}
