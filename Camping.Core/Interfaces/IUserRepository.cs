using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="User"/> entities.
/// Every lookup is a targeted database query - no method loads the whole table.
/// </summary>
public interface IUserRepository
{
    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>The user with this (already normalised, lower-case) email, or null.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Whether another user already has this (normalised) email.</summary>
    Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task UpdateAsync(User user, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
