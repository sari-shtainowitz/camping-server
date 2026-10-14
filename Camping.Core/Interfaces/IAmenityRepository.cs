using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="Amenity"/> entities.
/// </summary>
public interface IAmenityRepository
{
    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<Amenity>> GetPagedAsync(AmenityQueryParameters parameters, CancellationToken cancellationToken);

    Task<Amenity?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Whether another amenity already uses this name (case-insensitive).</summary>
    Task<bool> NameExistsAsync(string name, int? excludeAmenityId, CancellationToken cancellationToken);

    Task AddAsync(Amenity amenity, CancellationToken cancellationToken);

    Task UpdateAsync(Amenity amenity, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
