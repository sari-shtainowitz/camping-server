namespace Camping.Service.Exceptions;

/// <summary>
/// Thrown by <see cref="Interfaces.IAuthService"/> when a login attempt fails
/// (unknown email or wrong password). It carries a single generic message so the
/// API cannot be used to enumerate accounts.
/// The middleware maps this to an HTTP 401 response.
/// </summary>
public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message)
        : base(message)
    {
    }
}
