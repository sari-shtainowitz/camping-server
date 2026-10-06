using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="CampingSite"/> entities.
/// </summary>
public interface ICampingSiteRepository
{
    Task<List<CampingSite>> GetAllAsync();

    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<CampingSite>> GetPagedAsync(CampingSiteQueryParameters parameters);

    Task<CampingSite?> GetByIdAsync(int id);

    /// <summary>
    /// Loads a camping site together with its tents (eager-loaded via the
    /// Fluent API-configured one-to-many), so the delete guard can check for
    /// dependent tents without a separate full-table query.
    /// </summary>
    Task<CampingSite?> GetByIdWithTentsAsync(int id);

    Task AddAsync(CampingSite campingSite);

    Task UpdateAsync(CampingSite campingSite);

    Task DeleteAsync(int id);
}
