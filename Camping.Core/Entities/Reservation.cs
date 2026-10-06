using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camping.Core.Enums;

namespace Camping.Core.Entities;

public class Reservation
{
    public int Id { get; set; }

    // Foreign key: Reservation *--1 Tent (required)
    public int TentId { get; set; }

    // Foreign key: Reservation *--1 User (required)
    public int UserId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation: Reservation *--1 Tent
    public Tent Tent { get; set; } = null!;

    // Navigation: Reservation *--1 User
    public User User { get; set; } = null!;
}
