using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Business operations for tents.
/// </summary>
public interface ITentService
{
    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters, CancellationToken cancellationToken);

    Task<Tent> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Which tents are booked when, inside the window - visible to every guest,
    /// with no personal data (see TentOccupancyResponse).
    /// </summary>
    Task<IReadOnlyList<Reservation>> GetOccupancyAsync(
        OccupancyQueryParameters parameters, CancellationToken cancellationToken);

    Task<Tent> CreateAsync(Tent tent, CancellationToken cancellationToken);

    Task<Tent> UpdateAsync(int id, Tent tent, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
