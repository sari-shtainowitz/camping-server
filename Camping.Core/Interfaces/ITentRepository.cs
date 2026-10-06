using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="Tent"/> entities.
/// </summary>
public interface ITentRepository
{
    Task<List<Tent>> GetAllAsync();

    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters);

    Task<Tent?> GetByIdAsync(int id);

    Task AddAsync(Tent tent);

    Task UpdateAsync(Tent tent);

    Task DeleteAsync(int id);
}
