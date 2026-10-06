namespace Camping.Core.Querying;

/// <summary>
/// Paging, filtering and sorting inputs for the camping site list endpoint.
/// </summary>
public class CampingSiteQueryParameters : PaginationParameters
{
    // --- filtering (case-insensitive "contains") ---
    public string? Name { get; set; }
    public string? Address { get; set; }

    // --- sorting ---
    /// <summary>name (default) | address | createdAt. Case-insensitive.</summary>
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
