using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="Amenity"/> entities.
/// </summary>
public interface IAmenityRepository
{
    Task<List<Amenity>> GetAllAsync();

    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<Amenity>> GetPagedAsync(AmenityQueryParameters parameters);

    Task<Amenity?> GetByIdAsync(int id);

    Task AddAsync(Amenity amenity);

    Task UpdateAsync(Amenity amenity);

    Task DeleteAsync(int id);
}
