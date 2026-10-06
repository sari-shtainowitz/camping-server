using Camping.Core.Enums;

namespace Camping.Core.Querying;

/// <summary>
/// Paging, filtering and sorting inputs for the user list endpoint.
/// </summary>
public class UserQueryParameters : PaginationParameters
{
    // --- filtering ---
    public UserRole? Role { get; set; }

    /// <summary>Case-insensitive "contains" match on the email address.</summary>
    public string? Email { get; set; }

    // --- sorting ---
    /// <summary>fullName (default) | email | createdAt | role. Case-insensitive.</summary>
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
