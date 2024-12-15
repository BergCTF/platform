using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Berg.Api;

public static class Helpers
{
    /// <summary>
    /// Create a hash of an api key for storage
    /// </summary>
    /// <param name="apiKey">The API key to hash</param>
    /// <param name="userId">The user id of the user that the hash belongs to</param>
    /// <returns>The api key hash</returns>
    public static string GetApiKeyHash(string apiKey, Guid userId)
    {
        const int iterationCount = 100000;
        const int hashLength = 256 / 8;
        var saltBytes = userId.ToByteArray();
        var keyHash = KeyDerivation.Pbkdf2(apiKey, saltBytes, KeyDerivationPrf.HMACSHA512, iterationCount, hashLength);
        return Convert.ToBase64String(keyHash);
    }
}