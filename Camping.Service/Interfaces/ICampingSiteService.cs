using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for camping sites.
/// </summary>
public interface ICampingSiteService
{
    Task<IReadOnlyList<CampingSite>> GetAllAsync();

    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<CampingSite>> GetPagedAsync(CampingSiteQueryParameters parameters);

    Task<CampingSite> GetByIdAsync(int id);

    Task<CampingSite> CreateAsync(CampingSite campingSite);

    Task<CampingSite> UpdateAsync(int id, CampingSite campingSite);

    Task DeleteAsync(int id);
}
