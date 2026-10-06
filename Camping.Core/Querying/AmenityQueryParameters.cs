namespace Camping.Core.Querying;

/// <summary>
/// Paging, filtering and sorting inputs for the amenity list endpoint.
/// </summary>
public class AmenityQueryParameters : PaginationParameters
{
    // --- filtering (case-insensitive "contains") ---
    public string? Name { get; set; }

    // --- sorting ---
    /// <summary>name (default) | id. Case-insensitive.</summary>
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
