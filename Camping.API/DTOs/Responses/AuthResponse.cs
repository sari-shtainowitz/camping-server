namespace Camping.API.DTOs.Responses;

/// <summary>Returned by register and login: the bearer token plus basic identity info.</summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
