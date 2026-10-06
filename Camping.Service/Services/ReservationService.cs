using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;
using Camping.Service.Interfaces;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for reservations (the booking rules of the domain):
///   * the tent and the user must exist, and the tent must be active;
///   * the end date must be after the start date and the stay cannot start
///     in the past;
///   * the tent must be free for the requested dates - no overlap with an
///     existing non-cancelled reservation;
///   * the total price is calculated as nights x tent price per night;
///   * a new reservation is created with status Confirmed.
/// </summary>
public class ReservationService : IReservationService
{
    private readonly IReservationRepository _reservations;
    private readonly ITentRepository _tents;
    private readonly IUserRepository _users;

    public ReservationService(
        IReservationRepository reservations,
        ITentRepository tents,
        IUserRepository users)
    {
        _reservations = reservations;
        _tents = tents;
        _users = users;
    }

    public async Task<IReadOnlyList<Reservation>> GetAllAsync()
    {
        return await _reservations.GetAllAsync();
    }

    public Task<PagedResult<Reservation>> GetPagedAsync(ReservationQueryParameters parameters)
    {
        return _reservations.GetPagedAsync(parameters);
    }

    public async Task<Reservation> GetByIdAsync(int id)
    {
        var reservation = await _reservations.GetByIdAsync(id);
        if (reservation is null)
        {
            throw new NotFoundException($"Reservation {id} was not found.");
        }

        return reservation;
    }

    public async Task<Reservation> CreateAsync(Reservation reservation)
    {
        var tent = await RequireTentAsync(reservation.TentId);
        await RequireUserAsync(reservation.UserId);

        if (!tent.IsActive)
        {
            throw new BusinessRuleException("The selected tent is not active and cannot be booked.");
        }

        ValidateDates(reservation.StartDate, reservation.EndDate);

        reservation.Id = 0;
        reservation.Status = ReservationStatus.Confirmed;
        reservation.TotalPrice = CalculateTotalPrice(
            reservation.StartDate, reservation.EndDate, tent.PricePerNight);
        reservation.CreatedAt = DateTime.UtcNow;
        reservation.Tent = null!;
        reservation.User = null!;

        // Availability is re-checked inside the write transaction; the tent's
        // concurrency token turns a lost race into a ConcurrencyConflictException.
        await _reservations.AddTransactionalAsync(
            reservation,
            () => EnsureTentIsFreeAsync(
                reservation.TentId, reservation.StartDate, reservation.EndDate, excludeReservationId: null));
        return reservation;
    }

    public async Task<Reservation> UpdateAsync(int id, Reservation reservation)
    {
        var existing = await GetByIdAsync(id);

        var tent = await RequireTentAsync(reservation.TentId);
        await RequireUserAsync(reservation.UserId);

        if (!tent.IsActive)
        {
            throw new BusinessRuleException("The selected tent is not active and cannot be booked.");
        }

        ValidateDates(reservation.StartDate, reservation.EndDate);

        existing.TentId = reservation.TentId;
        existing.UserId = reservation.UserId;
        existing.StartDate = reservation.StartDate;
        existing.EndDate = reservation.EndDate;
        existing.Status = reservation.Status;
        existing.TotalPrice = CalculateTotalPrice(
            reservation.StartDate, reservation.EndDate, tent.PricePerNight);

        await _reservations.UpdateTransactionalAsync(
            existing,
            () => EnsureTentIsFreeAsync(
                existing.TentId, existing.StartDate, existing.EndDate, excludeReservationId: id));
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        await GetByIdAsync(id);
        await _reservations.DeleteAsync(id);
    }

    private async Task<Tent> RequireTentAsync(int tentId)
    {
        var tent = await _tents.GetByIdAsync(tentId);
        if (tent is null)
        {
            throw new BusinessRuleException($"Tent {tentId} does not exist.");
        }

        return tent;
    }

    private async Task RequireUserAsync(int userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            throw new BusinessRuleException($"User {userId} does not exist.");
        }
    }

    private static void ValidateDates(DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate)
        {
            throw new BusinessRuleException("The end date must be after the start date.");
        }

        if (startDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new BusinessRuleException("The start date cannot be in the past.");
        }
    }

    private async Task EnsureTentIsFreeAsync(
        int tentId, DateOnly startDate, DateOnly endDate, int? excludeReservationId)
    {
        var all = await _reservations.GetAllAsync();
        var overlaps = all.Any(r =>
            r.TentId == tentId &&
            r.Id != excludeReservationId &&
            r.Status != ReservationStatus.Cancelled &&
            startDate < r.EndDate &&
            r.StartDate < endDate);

        if (overlaps)
        {
            throw new BusinessRuleException("The tent is already reserved for the selected dates.");
        }
    }

    private static decimal CalculateTotalPrice(DateOnly startDate, DateOnly endDate, decimal pricePerNight)
    {
        var nights = endDate.DayNumber - startDate.DayNumber;
        return nights * pricePerNight;
    }
}
