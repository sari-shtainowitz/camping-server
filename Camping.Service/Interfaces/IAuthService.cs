using Camping.Core.Entities;

namespace Camping.Service.Interfaces;

/// <summary>
/// Registration and credential checking. Token creation is an API concern and
/// lives in the API layer.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new account. The password is hashed here; the role is always
    /// forced to Customer (self-registration cannot grant Admin).
    /// </summary>
    Task<User> RegisterAsync(User user, string password);

    /// <summary>
    /// Returns the user when the email/password pair is valid, otherwise throws
    /// <see cref="Exceptions.BusinessRuleException"/> with a generic message.
    /// </summary>
    Task<User> AuthenticateAsync(string email, string password);
}
