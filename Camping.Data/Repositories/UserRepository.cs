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

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    // Emails are stored normalised (trimmed, lower-case) by the services, so an
    // exact match is a case-insensitive match - and it can use the unique index.
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken cancellationToken)
    {
        return _context.Users
            .AnyAsync(u => u.Email == email && u.Id != excludeUserId, cancellationToken);
    }

    public async Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters, CancellationToken cancellationToken)
    {
        IQueryable<User> query = _context.Users.AsNoTracking();

        if (parameters.Role is not null)
        {
            query = query.Where(u => u.Role == parameters.Role);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Email))
        {
            var email = parameters.Email.Trim();
            // ILIKE: PostgreSQL's case-insensitive LIKE.
            query = query.Where(u => EF.Functions.ILike(u.Email, $"%{email}%"));
        }

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        return await query.ToPagedResultAsync(parameters.Page, parameters.PageSize, cancellationToken);
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

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user is null)
        {
            return;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
