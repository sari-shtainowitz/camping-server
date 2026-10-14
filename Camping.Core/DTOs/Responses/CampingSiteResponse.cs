namespace Camping.Core.DTOs.Responses;

/// <summary>
/// Outgoing representation of a camping site. Navigation data is not exposed here.
/// </summary>
public class CampingSiteResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Address { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
