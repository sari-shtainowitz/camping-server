using Camping.Core.Enums;

namespace Camping.Core.DTOs.Responses;

/// <summary>
/// Outgoing representation of a user. Navigation data is not exposed here.
/// </summary>
public class UserResponse
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; }
}
