using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="Tent"/> entities.
/// </summary>
public interface ITentRepository
{
    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters, CancellationToken cancellationToken);

    Task<Tent?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Whether another tent on the same site already uses this label (case-insensitive).</summary>
    Task<bool> LabelExistsAsync(
        int campingSiteId, string label, int? excludeTentId, CancellationToken cancellationToken);

    Task AddAsync(Tent tent, CancellationToken cancellationToken);

    Task UpdateAsync(Tent tent, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
