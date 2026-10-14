using System.Linq.Expressions;
using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Microsoft.EntityFrameworkCore;

namespace Camping.Data.Repositories;

/// <summary>
/// Basic EF Core data access for <see cref="Tent"/> entities.
/// </summary>
public class TentRepository : ITentRepository
{
    private readonly DbContext _context;

    public TentRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Tent?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Tents
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<bool> LabelExistsAsync(
        int campingSiteId, string label, int? excludeTentId, CancellationToken cancellationToken)
    {
        var lowered = label.ToLower();
        return _context.Tents.AnyAsync(
            t => t.CampingSiteId == campingSiteId &&
                 t.Id != excludeTentId &&
                 t.Label.ToLower() == lowered,
            cancellationToken);
    }

    public async Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters, CancellationToken cancellationToken)
    {
        IQueryable<Tent> query = _context.Tents.AsNoTracking();

        if (parameters.CampingSiteId is int campingSiteId)
        {
            query = query.Where(t => t.CampingSiteId == campingSiteId);
        }

        if (parameters.Type is not null)
        {
            query = query.Where(t => t.Type == parameters.Type);
        }

        if (parameters.IsActive is not null)
        {
            query = query.Where(t => t.IsActive == parameters.IsActive);
        }

        if (parameters.MinPricePerNight is decimal minPrice)
        {
            query = query.Where(t => t.PricePerNight >= minPrice);
        }

        if (parameters.MaxPricePerNight is decimal maxPrice)
        {
            query = query.Where(t => t.PricePerNight <= maxPrice);
        }

        if (parameters.MinCapacity is int minCapacity)
        {
            query = query.Where(t => t.Capacity >= minCapacity);
        }

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }

    private static IQueryable<Tent> ApplySort(IQueryable<Tent> query, string? sortBy, bool descending)
    {
        Expression<Func<Tent, object>> keySelector = sortBy?.Trim().ToLowerInvariant() switch
        {
            "label" => t => t.Label,
            "price" or "pricepernight" => t => t.PricePerNight,
            "capacity" => t => t.Capacity,
            "type" => t => t.Type,
            _ => t => t.Id
        };

        return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }

    public async Task AddAsync(Tent tent, CancellationToken cancellationToken)
    {
        await _context.Tents.AddAsync(tent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Tent tent, CancellationToken cancellationToken)
    {
        _context.Tents.Update(tent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var tent = await _context.Tents.FindAsync(new object[] { id }, cancellationToken);
        if (tent is null)
        {
            return;
        }

        _context.Tents.Remove(tent);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
