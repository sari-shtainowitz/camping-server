using System.Linq.Expressions;
using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Core.Exceptions;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Microsoft.EntityFrameworkCore;

namespace Camping.Data.Repositories;

/// <summary>
/// EF Core data access for <see cref="Reservation"/> entities, including the
/// transactional writes that guard the limited resource (the tent).
/// </summary>
public class ReservationRepository : IReservationRepository
{
    private readonly DbContext _context;

    public ReservationRepository(DbContext context)
    {
        _context = context;
    }

    // Read model: each reservation with its tent, the tent's camping site and the
    // guest, loaded in the SAME query (JOINs) - no per-row follow-up queries (N+1).
    private IQueryable<Reservation> WithDetails() =>
        _context.Reservations
            .AsNoTracking()
            .Include(r => r.Tent)
                .ThenInclude(t => t.CampingSite)
            .Include(r => r.User);

    public async Task<Reservation?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await WithDetails().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Reservation>> GetPagedAsync(
        ReservationQueryParameters parameters, CancellationToken cancellationToken)
    {
        IQueryable<Reservation> query = WithDetails();

        if (parameters.TentId is int tentId)
        {
            query = query.Where(r => r.TentId == tentId);
        }

        if (parameters.UserId is int userId)
        {
            query = query.Where(r => r.UserId == userId);
        }

        if (parameters.Status is not null)
        {
            query = query.Where(r => r.Status == parameters.Status);
        }

        if (parameters.StartsOnOrAfter is DateTime startsOnOrAfter)
        {
            var from = DateOnly.FromDateTime(startsOnOrAfter);
            query = query.Where(r => r.StartDate >= from);
        }

        if (parameters.EndsOnOrBefore is DateTime endsOnOrBefore)
        {
            var to = DateOnly.FromDateTime(endsOnOrBefore);
            query = query.Where(r => r.EndDate <= to);
        }

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }

    private static IQueryable<Reservation> ApplySort(
        IQueryable<Reservation> query, string? sortBy, bool descending)
    {
        Expression<Func<Reservation, object>> keySelector = sortBy?.Trim().ToLowerInvariant() switch
        {
            "enddate" => r => r.EndDate,
            "createdat" => r => r.CreatedAt,
            "totalprice" => r => r.TotalPrice,
            _ => r => r.StartDate
        };

        return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }

    // Same overlap rule as before ([start, end) ranges, cancelled bookings do not
    // count), but evaluated by the database as a single EXISTS query instead of
    // loading every reservation into memory.
    public Task<bool> HasOverlapAsync(
        int tentId, DateOnly startDate, DateOnly endDate, int? excludeReservationId,
        CancellationToken cancellationToken)
    {
        return _context.Reservations.AnyAsync(
            r => r.TentId == tentId &&
                 r.Id != excludeReservationId &&
                 r.Status != ReservationStatus.Cancelled &&
                 startDate < r.EndDate &&
                 r.StartDate < endDate,
            cancellationToken);
    }

    public Task<bool> ExistsForTentAsync(int tentId, CancellationToken cancellationToken)
    {
        return _context.Reservations.AnyAsync(r => r.TentId == tentId, cancellationToken);
    }

    public Task<bool> ExistsForUserAsync(int userId, CancellationToken cancellationToken)
    {
        return _context.Reservations.AnyAsync(r => r.UserId == userId, cancellationToken);
    }

    public Task AddTransactionalAsync(
        Reservation reservation, Func<Task> ensureTentIsAvailable, CancellationToken cancellationToken)
        => PersistWithTentGuardAsync(reservation, ensureTentIsAvailable, isNew: true, cancellationToken);

    public Task UpdateTransactionalAsync(
        Reservation reservation, Func<Task> ensureTentIsAvailable, CancellationToken cancellationToken)
        => PersistWithTentGuardAsync(reservation, ensureTentIsAvailable, isNew: false, cancellationToken);

    /// <summary>
    /// Runs the availability check and the reservation write in one transaction.
    /// The target tent is loaded (tracked) so <c>SaveChanges</c> rotates its
    /// concurrency token; a stale token turns into a
    /// <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    private async Task PersistWithTentGuardAsync(
        Reservation reservation, Func<Task> ensureTentIsAvailable, bool isNew,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await ensureTentIsAvailable();

        var tent = await _context.Tents.FirstOrDefaultAsync(t => t.Id == reservation.TentId, cancellationToken);
        if (tent is null)
        {
            throw new InvalidOperationException($"Tent {reservation.TentId} was not found.");
        }

        // The reservation may have been read with its navigation properties
        // (see WithDetails); never let EF try to save that object graph too.
        reservation.Tent = null!;
        reservation.User = null!;

        if (isNew)
        {
            _context.Reservations.Add(reservation);
        }
        else
        {
            _context.Reservations.Update(reservation);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyConflictException(
                "That tent was just booked by someone else. Please check availability and try again.", ex);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var reservation = await _context.Reservations.FindAsync(new object[] { id }, cancellationToken);
        if (reservation is null)
        {
            return;
        }

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
