namespace Camping.Core.DTOs.Responses;

/// <summary>
/// Outgoing representation of a camping site, with the amenities it offers
/// (the CampingSite >--&lt; Amenity many-to-many relationship, loaded with Include).
/// </summary>
public class CampingSiteResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Address { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<AmenityResponse> Amenities { get; set; } = new();
}
