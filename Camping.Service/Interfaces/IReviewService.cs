using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for guest reviews.
/// </summary>
public interface IReviewService
{
    /// <summary>Paged list, newest first, evaluated in the database.</summary>
    Task<PagedResult<Review>> GetPagedAsync(ReviewQueryParameters parameters, CancellationToken cancellationToken);

    Task<Review> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Creates a review authored by <paramref name="userId"/>.</summary>
    Task<Review> CreateAsync(int userId, int rating, string text, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
