using System.ComponentModel.DataAnnotations;
using Camping.Core.Enums;

namespace Camping.Core.DTOs.Requests;

/// <summary>
/// Incoming payload for creating or updating a user.
/// Server-managed fields (Id, CreatedAt) are intentionally not accepted.
/// </summary>
public class UserRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    [Required]
    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; set; }
}
