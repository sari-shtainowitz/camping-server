using System.ComponentModel.DataAnnotations;
using Camping.Core.Enums;

namespace Camping.API.DTOs.Requests;

/// <summary>
/// Incoming payload for creating or updating a tent.
/// Server-managed fields (Id, RowVersion) are intentionally not accepted.
/// </summary>
public class TentRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int CampingSiteId { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Label { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(TentType))]
    public TentType Type { get; set; }

    [Range(1, 20)]
    public int Capacity { get; set; }

    [Range(0.0, 100000.0)]
    public decimal PricePerNight { get; set; }

    public bool IsActive { get; set; } = true;
}
