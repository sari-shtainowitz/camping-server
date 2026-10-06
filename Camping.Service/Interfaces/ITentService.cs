using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for tents.
/// </summary>
public interface ITentService
{
    Task<IReadOnlyList<Tent>> GetAllAsync();

    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters);

    Task<Tent> GetByIdAsync(int id);

    Task<Tent> CreateAsync(Tent tent);

    Task<Tent> UpdateAsync(int id, Tent tent);

    Task DeleteAsync(int id);
}
