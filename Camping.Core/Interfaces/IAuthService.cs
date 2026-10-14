using Camping.Core.Entities;

namespace Camping.Core.Interfaces;

/// <summary>
/// Registration and login.
/// </summary>
public interface IAuthService
{
    /// <summary>Creates a Customer account; the password is stored only as a hash.</summary>
    Task<User> RegisterAsync(User user, string password, CancellationToken cancellationToken);

    /// <summary>Returns the user for valid credentials, otherwise throws.</summary>
    Task<User> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);
}
