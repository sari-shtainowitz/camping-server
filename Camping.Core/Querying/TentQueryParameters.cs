using Camping.Core.Enums;

namespace Camping.Core.Querying;

/// <summary>
/// Paging, filtering and sorting inputs for the tent list endpoint.
/// </summary>
public class TentQueryParameters : PaginationParameters
{
    // --- filtering ---
    public int? CampingSiteId { get; set; }
    public TentType? Type { get; set; }
    public bool? IsActive { get; set; }
    public decimal? MinPricePerNight { get; set; }
    public decimal? MaxPricePerNight { get; set; }
    public int? MinCapacity { get; set; }

    // --- sorting ---
    /// <summary>label | price | capacity | type | id (default). Case-insensitive.</summary>
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
