namespace Camping.API.Authentication;

/// <summary>
/// Strongly-typed view of the "Jwt" configuration section.
/// </summary>
public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Symmetric signing key. Must be at least 32 bytes for HMAC-SHA256.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
