using System.ComponentModel.DataAnnotations;

namespace Camping.API.DTOs.Requests;

/// <summary>
/// Self-registration payload. The account is always created with the Customer
/// role, so no role field is accepted here.
/// </summary>
public class RegisterRequest
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
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}
