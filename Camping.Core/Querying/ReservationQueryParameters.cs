using Camping.Core.Enums;

namespace Camping.Core.Querying;

/// <summary>
/// Paging, filtering and sorting inputs for the reservation list endpoint.
/// Dates are accepted as <see cref="DateTime"/> for query-string compatibility
/// and reduced to the date part when filtering.
/// </summary>
public class ReservationQueryParameters : PaginationParameters
{
    // --- filtering ---
    public int? TentId { get; set; }
    public int? UserId { get; set; }
    public ReservationStatus? Status { get; set; }
    public DateTime? StartsOnOrAfter { get; set; }
    public DateTime? EndsOnOrBefore { get; set; }

    // --- sorting ---
    /// <summary>startDate (default) | endDate | createdAt | totalPrice. Case-insensitive.</summary>
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
