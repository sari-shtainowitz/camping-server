using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Basic data access operations for <see cref="User"/> entities.
/// </summary>
public interface IUserRepository
{
    Task<List<User>> GetAllAsync();

    /// <summary>Database-side paged/filtered/sorted query.</summary>
    Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters);

    Task<User?> GetByIdAsync(int id);

    Task AddAsync(User user);

    Task UpdateAsync(User user);

    Task DeleteAsync(int id);
}
