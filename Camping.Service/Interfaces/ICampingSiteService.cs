using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for camping sites.
/// </summary>
public interface ICampingSiteService
{
    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<CampingSite>> GetPagedAsync(
        CampingSiteQueryParameters parameters, CancellationToken cancellationToken);

    Task<CampingSite> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<CampingSite> CreateAsync(CampingSite campingSite, CancellationToken cancellationToken);

    Task<CampingSite> UpdateAsync(int id, CampingSite campingSite, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
