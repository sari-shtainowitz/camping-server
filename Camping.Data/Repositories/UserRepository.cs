using System.Linq.Expressions;
using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Microsoft.EntityFrameworkCore;

namespace Camping.Data.Repositories;

/// <summary>
/// Basic EF Core data access for <see cref="User"/> entities.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly DbContext _context;

    public UserRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters)
    {
        IQueryable<User> query = _context.Users.AsNoTracking();

        if (parameters.Role is not null)
        {
            query = query.Where(u => u.Role == parameters.Role);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Email))
        {
            var email = parameters.Email.Trim();
            // SQL Server LIKE is case-insensitive under the server's CI collation.
            query = query.Where(u => EF.Functions.Like(u.Email, $"%{email}%"));
        }

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize);
    }

    private static IQueryable<User> ApplySort(IQueryable<User> query, string? sortBy, bool descending)
    {
        Expression<Func<User, object>> keySelector = sortBy?.Trim().ToLowerInvariant() switch
        {
            "email" => u => u.Email,
            "createdat" => u => u.CreatedAt,
            "role" => u => u.Role,
            _ => u => u.FullName
        };

        return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is null)
        {
            return;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }
}
