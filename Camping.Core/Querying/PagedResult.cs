namespace Camping.Core.Querying;

/// <summary>
/// A single page of results together with the metadata a client needs to page
/// through the rest of the set.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>1-based page number of this result.</summary>
    public int Page { get; set; }

    public int PageSize { get; set; }

    /// <summary>Total number of rows that matched the filter (before paging).</summary>
    public int TotalCount { get; set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
