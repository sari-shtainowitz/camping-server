using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;
using Camping.Service.Interfaces;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for users:
///   * full name and email are required;
///   * email is stored normalised (trimmed, lower-case) and must be unique;
///   * a user that still has reservations cannot be deleted.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IReservationRepository _reservations;

    public UserService(IUserRepository users, IReservationRepository reservations)
    {
        _users = users;
        _reservations = reservations;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync()
    {
        return await _users.GetAllAsync();
    }

    public Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters)
    {
        return _users.GetPagedAsync(parameters);
    }

    public async Task<User> GetByIdAsync(int id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null)
        {
            throw new NotFoundException($"User {id} was not found.");
        }

        return user;
    }

    public async Task<User> CreateAsync(User user)
    {
        var fullName = (user.FullName ?? string.Empty).Trim();
        var email = NormalizeEmail(user.Email);

        ValidateRequiredFields(fullName, email);
        await EnsureEmailIsUniqueAsync(email, excludeUserId: null);

        user.Id = 0;
        user.FullName = fullName;
        user.Email = email;
        user.PhoneNumber = NormalizeOptional(user.PhoneNumber);
        user.CreatedAt = DateTime.UtcNow;
        user.Reservations = new List<Reservation>();

        await _users.AddAsync(user);
        return user;
    }

    public async Task<User> UpdateAsync(int id, User user)
    {
        var existing = await GetByIdAsync(id);

        var fullName = (user.FullName ?? string.Empty).Trim();
        var email = NormalizeEmail(user.Email);

        ValidateRequiredFields(fullName, email);
        await EnsureEmailIsUniqueAsync(email, excludeUserId: id);

        existing.FullName = fullName;
        existing.Email = email;
        existing.PhoneNumber = NormalizeOptional(user.PhoneNumber);
        existing.Role = user.Role;

        await _users.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        await GetByIdAsync(id);

        var reservations = await _reservations.GetAllAsync();
        if (reservations.Any(r => r.UserId == id))
        {
            throw new BusinessRuleException("Cannot delete a user that still has reservations.");
        }

        await _users.DeleteAsync(id);
    }

    private static void ValidateRequiredFields(string fullName, string email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new BusinessRuleException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException("Email is required.");
        }
    }

    private async Task EnsureEmailIsUniqueAsync(string email, int? excludeUserId)
    {
        var all = await _users.GetAllAsync();
        var taken = all.Any(u =>
            u.Id != excludeUserId &&
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

        if (taken)
        {
            throw new BusinessRuleException($"Email '{email}' is already in use.");
        }
    }

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
