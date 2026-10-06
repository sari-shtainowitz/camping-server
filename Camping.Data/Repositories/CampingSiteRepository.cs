using System.Linq.Expressions;
using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Microsoft.EntityFrameworkCore;

namespace Camping.Data.Repositories;

/// <summary>
/// Basic EF Core data access for <see cref="CampingSite"/> entities.
/// </summary>
public class CampingSiteRepository : ICampingSiteRepository
{
    private readonly DbContext _context;

    public CampingSiteRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<List<CampingSite>> GetAllAsync()
    {
        return await _context.CampingSites
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<CampingSite?> GetByIdAsync(int id)
    {
        return await _context.CampingSites
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<CampingSite?> GetByIdWithTentsAsync(int id)
    {
        return await _context.CampingSites
            .Include(s => s.Tents)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<PagedResult<CampingSite>> GetPagedAsync(CampingSiteQueryParameters parameters)
    {
        IQueryable<CampingSite> query = _context.CampingSites.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(parameters.Name))
        {
            var name = parameters.Name.Trim();
            // SQL Server LIKE is case-insensitive under the server's CI collation.
            query = query.Where(s => EF.Functions.Like(s.Name, $"%{name}%"));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Address))
        {
            var address = parameters.Address.Trim();
            query = query.Where(s => EF.Functions.Like(s.Address, $"%{address}%"));
        }

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize);
    }

    private static IQueryable<CampingSite> ApplySort(
        IQueryable<CampingSite> query, string? sortBy, bool descending)
    {
        Expression<Func<CampingSite, object>> keySelector = sortBy?.Trim().ToLowerInvariant() switch
        {
            "address" => s => s.Address,
            "createdat" => s => s.CreatedAt,
            _ => s => s.Name
        };

        return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }

    public async Task AddAsync(CampingSite campingSite)
    {
        await _context.CampingSites.AddAsync(campingSite);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CampingSite campingSite)
    {
        _context.CampingSites.Update(campingSite);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var campingSite = await _context.CampingSites.FindAsync(id);
        if (campingSite is null)
        {
            return;
        }

        _context.CampingSites.Remove(campingSite);
        await _context.SaveChangesAsync();
    }
}
