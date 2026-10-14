using System.ComponentModel.DataAnnotations;

namespace Camping.Core.DTOs.Requests;

/// <summary>
/// Incoming payload for a new review. The author is taken from the caller's
/// JWT, so no user id or name is accepted here.
/// </summary>
public class ReviewRequest
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;
}
