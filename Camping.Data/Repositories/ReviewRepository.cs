using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Microsoft.EntityFrameworkCore;

namespace Camping.Data.Repositories;

/// <summary>
/// Basic EF Core data access for <see cref="Review"/> entities.
/// </summary>
public class ReviewRepository : IReviewRepository
{
    private readonly DbContext _context;

    public ReviewRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Review>> GetPagedAsync(
        ReviewQueryParameters parameters, CancellationToken cancellationToken)
    {
        IQueryable<Review> query = _context.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
    }

    public async Task<Review?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddAsync(Review review, CancellationToken cancellationToken)
    {
        await _context.Reviews.AddAsync(review, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews.FindAsync(new object[] { id }, cancellationToken);
        if (review is null)
        {
            return;
        }

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
