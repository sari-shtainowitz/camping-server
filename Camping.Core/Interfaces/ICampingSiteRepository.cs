using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="CampingSite"/> entities.
/// </summary>
public interface ICampingSiteRepository
{
    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<CampingSite>> GetPagedAsync(
        CampingSiteQueryParameters parameters, CancellationToken cancellationToken);

    /// <summary>The site row only - used before an update or delete.</summary>
    Task<CampingSite?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>The site with its amenities (many-to-many, Include) - for reading.</summary>
    Task<CampingSite?> GetWithAmenitiesAsync(int id, CancellationToken cancellationToken);

    /// <summary>Whether another site already uses this name (case-insensitive).</summary>
    Task<bool> NameExistsAsync(string name, int? excludeSiteId, CancellationToken cancellationToken);

    /// <summary>Whether the site still owns at least one tent.</summary>
    Task<bool> HasTentsAsync(int id, CancellationToken cancellationToken);

    Task AddAsync(CampingSite campingSite, CancellationToken cancellationToken);

    Task UpdateAsync(CampingSite campingSite, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
