using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Service.Interfaces;

/// <summary>
/// Business operations for users. All business validation lives in the
/// implementation, not in the API layer.
/// </summary>
public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync();

    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters);

    Task<User> GetByIdAsync(int id);

    Task<User> CreateAsync(User user);

    Task<User> UpdateAsync(int id, User user);

    Task DeleteAsync(int id);
}
