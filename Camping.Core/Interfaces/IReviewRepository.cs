using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="Review"/> entities.
/// Reads include the author (<see cref="Review.User"/>) so the API can show the name.
/// </summary>
public interface IReviewRepository
{
    /// <summary>Database-side paged query, newest first.</summary>
    Task<PagedResult<Review>> GetPagedAsync(ReviewQueryParameters parameters, CancellationToken cancellationToken);

    Task<Review?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task AddAsync(Review review, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
