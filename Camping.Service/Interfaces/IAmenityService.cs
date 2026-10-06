using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for amenities.
/// </summary>
public interface IAmenityService
{
    Task<IReadOnlyList<Amenity>> GetAllAsync();

    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<Amenity>> GetPagedAsync(AmenityQueryParameters parameters);

    Task<Amenity> GetByIdAsync(int id);

    Task<Amenity> CreateAsync(Amenity amenity);

    Task<Amenity> UpdateAsync(int id, Amenity amenity);

    Task DeleteAsync(int id);
}
