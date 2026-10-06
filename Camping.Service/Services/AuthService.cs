using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Core.Interfaces;
using Camping.Service.Exceptions;
using Camping.Service.Interfaces;
using Camping.Service.Security;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for authentication:
///   * full name, email and password are required; password must be >= 8 chars;
///   * email is normalised (trim + lower-case) and must be unique;
///   * a newly registered account is always a Customer;
///   * the password is stored only as a PBKDF2 hash;
///   * login failures return a single generic message (no account enumeration).
/// </summary>
public class AuthService : IAuthService
{
    private const int MinPasswordLength = 8;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IUserRepository users, IPasswordHasher passwordHasher)
    {
        _users = users;
        _passwordHasher = passwordHasher;
    }

    public async Task<User> RegisterAsync(User user, string password)
    {
        var fullName = (user.FullName ?? string.Empty).Trim();
        var email = (user.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new BusinessRuleException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException("Email is required.");
        }

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            throw new BusinessRuleException($"Password must be at least {MinPasswordLength} characters.");
        }

        var all = await _users.GetAllAsync();
        if (all.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleException($"Email '{email}' is already registered.");
        }

        var newUser = new User
        {
            Id = 0,
            FullName = fullName,
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(user.PhoneNumber) ? null : user.PhoneNumber.Trim(),
            Role = UserRole.Customer,
            PasswordHash = _passwordHasher.Hash(password),
            CreatedAt = DateTime.UtcNow,
            Reservations = new List<Reservation>()
        };

        await _users.AddAsync(newUser);
        return newUser;
    }

    public async Task<User> AuthenticateAsync(string email, string password)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();

        var all = await _users.GetAllAsync();
        var user = all.FirstOrDefault(u =>
            string.Equals(u.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (user is null ||
            string.IsNullOrEmpty(user.PasswordHash) ||
            !_passwordHasher.Verify(password ?? string.Empty, user.PasswordHash))
        {
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        return user;
    }
}
