using System.ComponentModel.DataAnnotations;

namespace Camping.Core.Entities;

public class CampingSite
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Navigation: CampingSite 1 --< Tent
    public ICollection<Tent> Tents { get; set; } = new List<Tent>();

    // Navigation: CampingSite >--< Amenity (many-to-many)
    public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
}
