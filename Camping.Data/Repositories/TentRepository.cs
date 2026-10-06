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

    public async Task<List<Tent>> GetAllAsync()
    {
        return await _context.Tents
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Tent?> GetByIdAsync(int id)
    {
        return await _context.Tents
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters)
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

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize);
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

    public async Task AddAsync(Tent tent)
    {
        await _context.Tents.AddAsync(tent);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Tent tent)
    {
        _context.Tents.Update(tent);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var tent = await _context.Tents.FindAsync(id);
        if (tent is null)
        {
            return;
        }

        _context.Tents.Remove(tent);
        await _context.SaveChangesAsync();
    }
}
