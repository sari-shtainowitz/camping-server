using System.Linq.Expressions;
using Camping.Core.Entities;
using Camping.Core.Exceptions;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Microsoft.EntityFrameworkCore;

namespace Camping.Data.Repositories;

/// <summary>
/// Basic EF Core data access for <see cref="Reservation"/> entities.
/// </summary>
public class ReservationRepository : IReservationRepository
{
    private readonly DbContext _context;

    public ReservationRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<List<Reservation>> GetAllAsync()
    {
        return await _context.Reservations
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Reservation?> GetByIdAsync(int id)
    {
        return await _context.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<PagedResult<Reservation>> GetPagedAsync(ReservationQueryParameters parameters)
    {
        IQueryable<Reservation> query = _context.Reservations.AsNoTracking();

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

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize);
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

    public async Task AddAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();
    }

    public Task AddTransactionalAsync(Reservation reservation, Func<Task> ensureTentIsAvailable)
        => PersistWithTentGuardAsync(reservation, ensureTentIsAvailable, isNew: true);

    public Task UpdateTransactionalAsync(Reservation reservation, Func<Task> ensureTentIsAvailable)
        => PersistWithTentGuardAsync(reservation, ensureTentIsAvailable, isNew: false);

    /// <summary>
    /// Runs the availability check and the reservation write in one transaction.
    /// The target tent is loaded (tracked) so <c>SaveChanges</c> rotates its
    /// concurrency token; a stale token turns into a
    /// <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    private async Task PersistWithTentGuardAsync(
        Reservation reservation, Func<Task> ensureTentIsAvailable, bool isNew)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        await ensureTentIsAvailable();

        var tent = await _context.Tents.FirstOrDefaultAsync(t => t.Id == reservation.TentId);
        if (tent is null)
        {
            throw new InvalidOperationException($"Tent {reservation.TentId} was not found.");
        }

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
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync();
            throw new ConcurrencyConflictException(
                "That tent was just booked by someone else. Please check availability and try again.", ex);
        }
    }

    public async Task DeleteAsync(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation is null)
        {
            return;
        }

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();
    }
}
