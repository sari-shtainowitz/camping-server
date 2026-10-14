using Camping.Core.Entities;
using Camping.Core.Querying;

namespace Camping.Core.Interfaces;

/// <summary>
/// Business operations for users.
/// </summary>
public interface IUserService
{
    /// <summary>Paged/filtered/sorted list, evaluated in the database.</summary>
    Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters, CancellationToken cancellationToken);

    Task<User> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<User> UpdateAsync(int id, User user, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
