using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Data access for <see cref="Reservation"/> entities, including the
/// transactional writes that guard the limited resource (the tent).
/// </summary>
public interface IReservationRepository
{
    /// <summary>
    /// Database-side paged/filtered/sorted query. Each reservation comes with its
    /// tent, the tent's camping site and the guest (Include / ThenInclude).
    /// </summary>
    Task<PagedResult<Reservation>> GetPagedAsync(
        ReservationQueryParameters parameters, CancellationToken cancellationToken);

    /// <summary>A single reservation with its tent, camping site and guest.</summary>
    Task<Reservation?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a non-cancelled reservation for this tent overlaps [startDate, endDate).
    /// Runs as one EXISTS query in the database.
    /// </summary>
    Task<bool> HasOverlapAsync(
        int tentId, DateOnly startDate, DateOnly endDate, int? excludeReservationId,
        CancellationToken cancellationToken);

    Task<bool> ExistsForTentAsync(int tentId, CancellationToken cancellationToken);

    Task<bool> ExistsForUserAsync(int userId, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the reservation inside a transaction that first runs
    /// <paramref name="ensureTentIsAvailable"/> and rotates the tent's
    /// concurrency token, so a racing booking for the same tent fails.
    /// </summary>
    Task AddTransactionalAsync(
        Reservation reservation, Func<Task> ensureTentIsAvailable, CancellationToken cancellationToken);

    /// <summary>Same guard as <see cref="AddTransactionalAsync"/>, for an update.</summary>
    Task UpdateTransactionalAsync(
        Reservation reservation, Func<Task> ensureTentIsAvailable, CancellationToken cancellationToken);
}
