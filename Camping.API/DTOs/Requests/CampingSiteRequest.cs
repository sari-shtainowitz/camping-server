using System.ComponentModel.DataAnnotations;

namespace Camping.API.DTOs.Requests;

/// <summary>
/// Incoming payload for creating or updating a camping site.
/// Server-managed fields (Id, CreatedAt) are intentionally not accepted.
/// </summary>
public class CampingSiteRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 5)]
    public string Address { get; set; } = string.Empty;
}
