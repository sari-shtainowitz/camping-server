namespace Camping.Core.Querying;

/// <summary>
/// Date window for the public tent-occupancy endpoint. Accepted as
/// <see cref="DateTime"/> for query-string binding and reduced to the date part.
/// Both are optional: the window defaults to today .. one year ahead.
/// </summary>
public class OccupancyQueryParameters
{
    /// <summary>First day of the window (inclusive).</summary>
    public DateTime? From { get; set; }

    /// <summary>Last day of the window (exclusive), like a check-out date.</summary>
    public DateTime? To { get; set; }
}
