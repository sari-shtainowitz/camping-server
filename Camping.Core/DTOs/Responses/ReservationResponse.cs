using Camping.Core.Enums;

namespace Camping.Core.DTOs.Responses;

/// <summary>
/// Outgoing representation of a reservation. Related data is flattened into a
/// few display fields (filled when the reservation was read with its tent,
/// camping site and guest - see ReservationRepository's Include/ThenInclude);
/// they are null on the response to a create or update.
/// </summary>
public class ReservationResponse
{
    public string? TentLabel { get; set; }

    public string? CampingSiteName { get; set; }

    public string? GuestName { get; set; }

    public int Id { get; set; }

    public int TentId { get; set; }

    public int UserId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ReservationStatus Status { get; set; }

    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }
}
