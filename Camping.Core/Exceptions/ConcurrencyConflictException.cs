namespace Camping.Core.Exceptions;

/// <summary>
/// Raised when a write to the limited resource (a tent) loses an optimistic
/// concurrency check because another transaction rotated its token first.
/// The API surfaces this as HTTP 409 Conflict.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
