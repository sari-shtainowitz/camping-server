namespace Camping.Service.Exceptions;

/// <summary>
/// Thrown by a service when a requested entity does not exist.
/// A later step maps this to an HTTP 404 response.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}
