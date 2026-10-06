using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Camping.Core.Enums;

namespace Camping.Core.Entities;

public class Tent
{
    public int Id { get; set; }

    // Foreign key: Tent *--1 CampingSite (required)
    public int CampingSiteId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Label { get; set; } = string.Empty;

    public TentType Type { get; set; }

    public int Capacity { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal PricePerNight { get; set; }

    public bool IsActive { get; set; } = true;

    // Optimistic concurrency token for the limited resource (Step 11).
    // Application-managed (not store-generated): the value is rotated centrally
    // in DbContext.SaveChanges whenever the tent - or a reservation that points
    // at it - is written, so PostgreSQL does not need a native row-version type.
    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }

    // Navigation: Tent *--1 CampingSite
    public CampingSite CampingSite { get; set; } = null!;

    // Navigation: Tent 1 --< Reservation
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
