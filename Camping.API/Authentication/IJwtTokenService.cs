using Camping.Core.Entities;

namespace Camping.API.Authentication;

/// <summary>Creates signed JWT access tokens for authenticated users.</summary>
public interface IJwtTokenService
{
    TokenResult CreateToken(User user);
}

/// <summary>A freshly issued access token and its expiry (UTC).</summary>
public record TokenResult(string AccessToken, DateTime ExpiresAtUtc);
