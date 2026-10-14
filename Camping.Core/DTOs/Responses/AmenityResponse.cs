namespace Camping.Core.DTOs.Responses;

/// <summary>
/// Outgoing representation of an amenity. Navigation data is not exposed here.
/// </summary>
public class AmenityResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
