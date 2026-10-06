namespace Camping.Core.Querying;

/// <summary>
/// Common paging inputs for list endpoints. The property setters clamp the
/// incoming values, so a caller can never request page 0, a negative size, or a
/// page larger than <see cref="MaxPageSize"/>.
/// </summary>
public class PaginationParameters
{
    /// <summary>Largest page size a client may request.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Page size used when the caller does not supply one.</summary>
    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>1-based page number. Values below 1 are treated as 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Items per page. Clamped to [1, <see cref="MaxPageSize"/>].</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }
}
