using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;
using Camping.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for reviews:
///   * the author is the authenticated caller and must exist - the name shown
///     is always the author's account name, never a value the client supplies;
///   * the rating is 1-5 stars and the text is required (max 1000 chars).
/// </summary>
public class ReviewService : IReviewService
{
    public const int MaxTextLength = 1000;

    private readonly IReviewRepository _reviews;
    private readonly IUserRepository _users;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(IReviewRepository reviews, IUserRepository users, ILogger<ReviewService> logger)
    {
        _reviews = reviews;
        _users = users;
        _logger = logger;
    }

    public Task<PagedResult<Review>> GetPagedAsync(ReviewQueryParameters parameters, CancellationToken cancellationToken)
    {
        return _reviews.GetPagedAsync(parameters, cancellationToken);
    }

    public async Task<Review> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetByIdAsync(id, cancellationToken);
        if (review is null)
        {
            throw new NotFoundException($"Review {id} was not found.");
        }

        return review;
    }

    public async Task<Review> CreateAsync(int userId, int rating, string text, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessRuleException($"User {userId} does not exist.");
        }

        if (rating < 1 || rating > 5)
        {
            throw new BusinessRuleException("The rating must be between 1 and 5.");
        }

        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new BusinessRuleException("The review text is required.");
        }

        if (trimmed.Length > MaxTextLength)
        {
            throw new BusinessRuleException($"The review text cannot exceed {MaxTextLength} characters.");
        }

        var review = new Review
        {
            UserId = userId,
            Rating = rating,
            Text = trimmed,
            CreatedAt = DateTime.UtcNow
        };

        await _reviews.AddAsync(review, cancellationToken);
        _logger.LogInformation("Review {ReviewId} ({Rating} stars) posted by user {UserId}", review.Id, rating, userId);

        // Attach the author after saving so the response can carry the name.
        review.User = user;
        return review;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await GetByIdAsync(id, cancellationToken);
        await _reviews.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Review {ReviewId} deleted", id);
    }
}
