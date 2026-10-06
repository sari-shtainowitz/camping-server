namespace Camping.Service.Exceptions;

/// <summary>
/// Thrown by a service when a business rule or business validation fails
/// (for example a duplicate name, an overlapping reservation, or an attempt
/// to delete an entity that is still referenced).
/// A later step maps this to an HTTP 400/409 response.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}
