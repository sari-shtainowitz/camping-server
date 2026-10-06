using System.ComponentModel.DataAnnotations;

namespace Camping.API.DTOs.Requests;

/// <summary>
/// Incoming payload for creating or updating an amenity.
/// The server-managed Id is intentionally not accepted.
/// </summary>
public class AmenityRequest
{
    [Required]
    [StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
