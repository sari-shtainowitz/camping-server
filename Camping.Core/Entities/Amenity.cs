using System.ComponentModel.DataAnnotations;

namespace Camping.Core.Entities;

public class Amenity
{
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    // Navigation: Amenity >--< CampingSite (many-to-many)
    public ICollection<CampingSite> CampingSites { get; set; } = new List<CampingSite>();
}
