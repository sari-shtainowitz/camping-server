using System.ComponentModel.DataAnnotations;

namespace Camping.Core.Entities;

public class Review
{
    public int Id { get; set; }

    // Foreign key: Review *--1 User (the author - always the authenticated caller)
    public int UserId { get; set; }

    // 1-5 stars
    public int Rating { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Navigation: Review *--1 User
    public User User { get; set; } = null!;
}
