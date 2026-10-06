using Camping.Core.Enums;

namespace Camping.API.DTOs.Responses;

/// <summary>
/// Outgoing representation of a reservation. Navigation data is not exposed here.
/// </summary>
public class ReservationResponse
{
    public int Id { get; set; }

    public int TentId { get; set; }

    public int UserId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ReservationStatus Status { get; set; }

    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }
}
