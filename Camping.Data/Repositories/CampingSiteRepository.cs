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

    public async Task<CampingSite?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.CampingSites
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, int? excludeSiteId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        return _context.CampingSites.AnyAsync(
            s => s.Id != excludeSiteId && s.Name.ToLower() == lowered,
            cancellationToken);
    }

    public Task<bool> HasTentsAsync(int id, CancellationToken cancellationToken)
    {
        return _context.Tents.AnyAsync(t => t.CampingSiteId == id, cancellationToken);
    }

    public async Task<PagedResult<CampingSite>> GetPagedAsync(
        CampingSiteQueryParameters parameters, CancellationToken cancellationToken)
    {
        IQueryable<CampingSite> query = _context.CampingSites.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(parameters.Name))
        {
            var name = parameters.Name.Trim();
            // ILIKE: PostgreSQL's case-insensitive LIKE.
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{name}%"));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Address))
        {
            var address = parameters.Address.Trim();
            query = query.Where(s => EF.Functions.ILike(s.Address, $"%{address}%"));
        }

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
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

    public async Task AddAsync(CampingSite campingSite, CancellationToken cancellationToken)
    {
        await _context.CampingSites.AddAsync(campingSite, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CampingSite campingSite, CancellationToken cancellationToken)
    {
        _context.CampingSites.Update(campingSite);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var campingSite = await _context.CampingSites.FindAsync(new object[] { id }, cancellationToken);
        if (campingSite is null)
        {
            return;
        }

        _context.CampingSites.Remove(campingSite);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
