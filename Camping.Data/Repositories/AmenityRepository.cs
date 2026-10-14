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

    public async Task<Amenity?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Amenities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, int? excludeAmenityId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        return _context.Amenities.AnyAsync(
            a => a.Id != excludeAmenityId && a.Name.ToLower() == lowered,
            cancellationToken);
    }

    public async Task<PagedResult<Amenity>> GetPagedAsync(
        AmenityQueryParameters parameters, CancellationToken cancellationToken)
    {
        IQueryable<Amenity> query = _context.Amenities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(parameters.Name))
        {
            var name = parameters.Name.Trim();
            // ILIKE: PostgreSQL's case-insensitive LIKE.
            query = query.Where(a => EF.Functions.ILike(a.Name, $"%{name}%"));
        }

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
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

    public async Task AddAsync(Amenity amenity, CancellationToken cancellationToken)
    {
        await _context.Amenities.AddAsync(amenity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Amenity amenity, CancellationToken cancellationToken)
    {
        _context.Amenities.Update(amenity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var amenity = await _context.Amenities.FindAsync(new object[] { id }, cancellationToken);
        if (amenity is null)
        {
            return;
        }

        _context.Amenities.Remove(amenity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
