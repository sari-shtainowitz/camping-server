namespace Camping.Service.Security;

/// <summary>
/// Hashes and verifies user passwords. Kept as an abstraction so the service
/// layer never depends on a concrete crypto implementation.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a self-describing hash string for <paramref name="password"/>.</summary>
    string Hash(string password);

    /// <summary>Returns true when <paramref name="password"/> matches <paramref name="hash"/>.</summary>
    bool Verify(string password, string hash);
}
