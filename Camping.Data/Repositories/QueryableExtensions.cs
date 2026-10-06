using Camping.Core.Querying;
using Microsoft.EntityFrameworkCore;

namespace Camping.Data.Repositories;

/// <summary>
/// Shared paging helper. <see cref="ToPagedResultAsync"/> runs both the row
/// count and the <c>Skip</c>/<c>Take</c> window on the database - the caller
/// passes an <see cref="IQueryable{T}"/> that has already had its filters and
/// ordering applied, and nothing is materialised until the two queries here run.
/// </summary>
internal static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize)
    {
        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
