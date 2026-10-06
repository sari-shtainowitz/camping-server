using Camping.Core.Enums;

namespace Camping.API.DTOs.Responses;

/// <summary>
/// Outgoing representation of a tent. The concurrency token and navigation
/// data are not exposed here.
/// </summary>
public class TentResponse
{
    public int Id { get; set; }

    public int CampingSiteId { get; set; }

    public string Label { get; set; } = string.Empty;

    public TentType Type { get; set; }

    public int Capacity { get; set; }

    public decimal PricePerNight { get; set; }

    public bool IsActive { get; set; }
}
