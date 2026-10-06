using System.Linq.Expressions;
using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Microsoft.EntityFrameworkCore;

namespace Camping.Data.Repositories;

/// <summary>
/// Basic EF Core data access for <see cref="Amenity"/> entities.
/// </summary>
public class AmenityRepository : IAmenityRepository
{
    private readonly DbContext _context;

    public AmenityRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<List<Amenity>> GetAllAsync()
    {
        return await _context.Amenities
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Amenity?> GetByIdAsync(int id)
    {
        return await _context.Amenities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<PagedResult<Amenity>> GetPagedAsync(AmenityQueryParameters parameters)
    {
        IQueryable<Amenity> query = _context.Amenities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(parameters.Name))
        {
            var name = parameters.Name.Trim();
            // SQL Server LIKE is case-insensitive under the server's CI collation.
            query = query.Where(a => EF.Functions.Like(a.Name, $"%{name}%"));
        }

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize);
    }

    private static IQueryable<Amenity> ApplySort(
        IQueryable<Amenity> query, string? sortBy, bool descending)
    {
        Expression<Func<Amenity, object>> keySelector = sortBy?.Trim().ToLowerInvariant() switch
        {
            "id" => a => a.Id,
            _ => a => a.Name
        };

        return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }

    public async Task AddAsync(Amenity amenity)
    {
        await _context.Amenities.AddAsync(amenity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Amenity amenity)
    {
        _context.Amenities.Update(amenity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var amenity = await _context.Amenities.FindAsync(id);
        if (amenity is null)
        {
            return;
        }

        _context.Amenities.Remove(amenity);
        await _context.SaveChangesAsync();
    }
}
