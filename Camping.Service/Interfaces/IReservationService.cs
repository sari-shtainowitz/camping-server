using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for reservations. This service owns the booking rules
/// (date validation, tent availability, price calculation).
/// </summary>
public interface IReservationService
{
    Task<IReadOnlyList<Reservation>> GetAllAsync();

    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<Reservation>> GetPagedAsync(ReservationQueryParameters parameters);

    Task<Reservation> GetByIdAsync(int id);

    Task<Reservation> CreateAsync(Reservation reservation);

    Task<Reservation> UpdateAsync(int id, Reservation reservation);

    Task DeleteAsync(int id);
}
