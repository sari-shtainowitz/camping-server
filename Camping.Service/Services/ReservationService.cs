using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;
using Microsoft.Extensions.Logging;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for reservations (the booking rules of the domain):
///   * the tent and the user must exist, and the tent must be active;
///   * the end date must be after the start date and the stay cannot start
///     in the past;
///   * the tent must be free for the requested dates - no overlap with an
///     existing non-cancelled reservation (a clash is a 409 Conflict);
///   * the total price is calculated as nights x tent price per night;
///   * a new reservation is created with status Confirmed.
/// </summary>
public class ReservationService : IReservationService
{
    private readonly IReservationRepository _reservations;
    private readonly ITentRepository _tents;
    private readonly IUserRepository _users;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(
        IReservationRepository reservations,
        ITentRepository tents,
        IUserRepository users,
        ILogger<ReservationService> logger)
    {
        _reservations = reservations;
        _tents = tents;
        _users = users;
        _logger = logger;
    }

    public Task<PagedResult<Reservation>> GetPagedAsync(
        ReservationQueryParameters parameters, CancellationToken cancellationToken)
    {
        return _reservations.GetPagedAsync(parameters, cancellationToken);
    }

    public async Task<Reservation> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(id, cancellationToken);
        if (reservation is null)
        {
            throw new NotFoundException($"Reservation {id} was not found.");
        }

        return reservation;
    }

    public async Task<Reservation> CreateAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var tent = await RequireTentAsync(reservation.TentId, cancellationToken);
        await RequireUserAsync(reservation.UserId, cancellationToken);

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
                reservation.TentId, reservation.StartDate, reservation.EndDate,
                excludeReservationId: null, cancellationToken),
            cancellationToken);

        _logger.LogInformation(
            "Reservation {ReservationId} created: tent {TentId}, user {UserId}, {StartDate} to {EndDate}",
            reservation.Id, reservation.TentId, reservation.UserId, reservation.StartDate, reservation.EndDate);

        return reservation;
    }

    public async Task<Reservation> UpdateAsync(int id, Reservation reservation, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(id, cancellationToken);

        var tent = await RequireTentAsync(reservation.TentId, cancellationToken);
        await RequireUserAsync(reservation.UserId, cancellationToken);

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
                existing.TentId, existing.StartDate, existing.EndDate,
                excludeReservationId: id, cancellationToken),
            cancellationToken);

        _logger.LogInformation("Reservation {ReservationId} updated", id);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await GetByIdAsync(id, cancellationToken);
        await _reservations.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Reservation {ReservationId} deleted", id);
    }

    private async Task<Tent> RequireTentAsync(int tentId, CancellationToken cancellationToken)
    {
        var tent = await _tents.GetByIdAsync(tentId, cancellationToken);
        if (tent is null)
        {
            throw new BusinessRuleException($"Tent {tentId} does not exist.");
        }

        return tent;
    }

    private async Task RequireUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
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
        int tentId, DateOnly startDate, DateOnly endDate, int? excludeReservationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Checking availability of tent {TentId} for {StartDate} to {EndDate}", tentId, startDate, endDate);

        var overlaps = await _reservations.HasOverlapAsync(
            tentId, startDate, endDate, excludeReservationId, cancellationToken);

        if (overlaps)
        {
            // The request is valid on its own but clashes with the tent's current
            // bookings - that is a 409 Conflict, not a 400.
            throw new ResourceConflictException("The tent is already reserved for the selected dates.");
        }
    }

    private static decimal CalculateTotalPrice(DateOnly startDate, DateOnly endDate, decimal pricePerNight)
    {
        var nights = endDate.DayNumber - startDate.DayNumber;
        return nights * pricePerNight;
    }
}
