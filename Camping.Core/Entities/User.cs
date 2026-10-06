using System.ComponentModel.DataAnnotations;
using Camping.Core.Enums;

namespace Camping.Core.Entities;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    // Set during registration (Step 8). Nullable so the pre-auth seed users
    // remain valid; those accounts simply cannot log in.
    [MaxLength(500)]
    public string? PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Customer;

    public DateTime CreatedAt { get; set; }

    // Navigation: User 1 --< Reservation
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
