using Camping.Core.Interfaces;
using System.Security.Cryptography;

namespace Camping.Service.Security;

/// <summary>
/// PBKDF2 (HMAC-SHA256) password hasher using only the base class library,
/// so the service layer needs no extra NuGet package.
///
/// Stored format:  pbkdf2.sha256.{iterations}.{saltBase64}.{keyBase64}
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;        // 128-bit salt
    private const int KeySize = 32;         // 256-bit derived key
    private const int Iterations = 210_000; // OWASP-recommended for PBKDF2-SHA256
    private const string Prefix = "pbkdf2.sha256";

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password must not be empty.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join('.', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('.');
        if (parts.Length != 5 || $"{parts[0]}.{parts[1]}" != Prefix)
        {
            return false;
        }

        if (!int.TryParse(parts[2], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expectedKey = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
