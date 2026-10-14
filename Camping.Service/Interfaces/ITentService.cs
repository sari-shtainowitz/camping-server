using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for tents.
/// </summary>
public interface ITentService
{
    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters, CancellationToken cancellationToken);

    Task<Tent> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<Tent> CreateAsync(Tent tent, CancellationToken cancellationToken);

    Task<Tent> UpdateAsync(int id, Tent tent, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
