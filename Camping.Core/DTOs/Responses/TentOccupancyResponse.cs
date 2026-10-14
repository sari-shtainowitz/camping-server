namespace Camping.Core.DTOs.Responses;

/// <summary>
/// One booked date range of a tent, as shown to every signed-in user so they can
/// see which tents are taken. Deliberately carries NO personal data - no guest,
/// no reservation id, no price - only which tent and which nights.
/// </summary>
public class TentOccupancyResponse
{
    public int TentId { get; set; }

    /// <summary>Check-in date (first booked night).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Check-out date (the tent is free again from this day).</summary>
    public DateOnly EndDate { get; set; }
}
