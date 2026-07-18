using System.Security.Cryptography;
using NorthernLink.Identity.Application.Abstractions;

namespace NorthernLink.Identity.Infrastructure.Auth;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing (no new package — built into .NET). Stored as
/// <c>{iterations}.{saltBase64}.{hashBase64}</c> so the iteration count and salt travel with
/// the hash, letting a future bump to <see cref="Iterations"/> verify old hashes unchanged.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>OWASP's current PBKDF2-HMAC-SHA256 recommendation (2023 cheat sheet).</summary>
    private const int Iterations = 210_000;

    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
