using System.ComponentModel.DataAnnotations;

namespace Camping.Core.DTOs.Requests;

/// <summary>Credentials for exchanging email + password for a JWT.</summary>
public class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}
