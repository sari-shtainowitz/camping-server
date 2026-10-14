namespace Camping.Core.DTOs.Responses;

/// <summary>
/// Outgoing representation of a review. Only the author's display name is
/// exposed - never their email or id.
/// </summary>
public class ReviewResponse
{
    public int Id { get; set; }

    public int Rating { get; set; }

    public string Text { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
