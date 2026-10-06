using Camping.Core.Entities;
using Camping.Core.Exceptions;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="Reservation"/> entities.
/// </summary>
public interface IReservationRepository
{
    Task<List<Reservation>> GetAllAsync();

    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<Reservation>> GetPagedAsync(ReservationQueryParameters parameters);

    Task<Reservation?> GetByIdAsync(int id);

    Task AddAsync(Reservation reservation);

    Task UpdateAsync(Reservation reservation);

    Task DeleteAsync(int id);

    /// <summary>
    /// Inserts a reservation inside a database transaction that also rotates the
    /// target tent's concurrency token (Step 11). <paramref name="ensureTentIsAvailable"/>
    /// runs inside that transaction and must throw if the tent is not free, so
    /// the availability check and the write share one consistent snapshot.
    /// Throws <see cref="ConcurrencyConflictException"/> when another transaction
    /// booked the same tent first.
    /// </summary>
    Task AddTransactionalAsync(Reservation reservation, Func<Task> ensureTentIsAvailable);

    /// <summary>
    /// Transactional, concurrency-guarded counterpart of <see cref="UpdateAsync"/>.
    /// Same protection as <see cref="AddTransactionalAsync"/>.
    /// </summary>
    Task UpdateTransactionalAsync(Reservation reservation, Func<Task> ensureTentIsAvailable);
}
