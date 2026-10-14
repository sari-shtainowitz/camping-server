namespace Camping.Service.Exceptions;

/// <summary>
/// Thrown by a service when a request is valid on its own but conflicts with
/// the current state of the data - for example booking a tent that is already
/// reserved for those dates. The API maps this to HTTP 409 Conflict, the same
/// status a lost concurrency race gets (see ConcurrencyConflictException).
/// </summary>
public class ResourceConflictException : Exception
{
    public ResourceConflictException(string message)
        : base(message)
    {
    }
}
