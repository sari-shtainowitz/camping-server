using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for amenities.
/// </summary>
public interface IAmenityService
{
    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<Amenity>> GetPagedAsync(AmenityQueryParameters parameters, CancellationToken cancellationToken);

    Task<Amenity> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<Amenity> CreateAsync(Amenity amenity, CancellationToken cancellationToken);

    Task<Amenity> UpdateAsync(int id, Amenity amenity, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
