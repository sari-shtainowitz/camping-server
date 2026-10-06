using System.ComponentModel.DataAnnotations;
using Camping.Core.Enums;

namespace Camping.API.DTOs.Requests;

/// <summary>
/// Incoming payload for creating or updating a reservation.
/// Server-managed fields (Id, CreatedAt, TotalPrice) are intentionally not accepted;
/// TotalPrice is calculated server-side in a later step.
/// </summary>
public class ReservationRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int TentId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Required]
    [EnumDataType(typeof(ReservationStatus))]
    public ReservationStatus Status { get; set; }
}
