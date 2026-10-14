using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for users (accounts are created through AuthService.RegisterAsync):
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

    public Task<PagedResult<User>> GetPagedAsync(UserQueryParameters parameters, CancellationToken cancellationToken)
    {
        return _users.GetPagedAsync(parameters, cancellationToken);
    }

    public async Task<User> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User {id} was not found.");
        }

        return user;
    }

    public async Task<User> UpdateAsync(int id, User user, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(id, cancellationToken);

        var fullName = (user.FullName ?? string.Empty).Trim();
        var email = NormalizeEmail(user.Email);

        ValidateRequiredFields(fullName, email);

        if (await _users.EmailExistsAsync(email, excludeUserId: id, cancellationToken))
        {
            throw new BusinessRuleException($"Email '{email}' is already in use.");
        }

        existing.FullName = fullName;
        existing.Email = email;
        existing.PhoneNumber = NormalizeOptional(user.PhoneNumber);
        existing.Role = user.Role;

        await _users.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await GetByIdAsync(id, cancellationToken);

        if (await _reservations.ExistsForUserAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("Cannot delete a user that still has reservations.");
        }

        await _users.DeleteAsync(id, cancellationToken);
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

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
