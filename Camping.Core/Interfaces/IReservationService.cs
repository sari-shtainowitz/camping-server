using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Business operations for reservations. This service owns the booking rules
/// (date validation, tent availability, price calculation).
/// </summary>
public interface IReservationService
{
    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<Reservation>> GetPagedAsync(
        ReservationQueryParameters parameters, CancellationToken cancellationToken);

    Task<Reservation> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<Reservation> CreateAsync(Reservation reservation, CancellationToken cancellationToken);

    Task<Reservation> UpdateAsync(int id, Reservation reservation, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
