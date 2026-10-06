using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;
using Camping.Service.Interfaces;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for tents:
///   * the referenced camping site must exist;
///   * the label is required and must be unique within its camping site;
///   * capacity must be at least 1 and price per night cannot be negative;
///   * a tent that still has reservations cannot be deleted.
/// </summary>
public class TentService : ITentService
{
    private readonly ITentRepository _tents;
    private readonly ICampingSiteRepository _sites;
    private readonly IReservationRepository _reservations;

    public TentService(
        ITentRepository tents,
        ICampingSiteRepository sites,
        IReservationRepository reservations)
    {
        _tents = tents;
        _sites = sites;
        _reservations = reservations;
    }

    public async Task<IReadOnlyList<Tent>> GetAllAsync()
    {
        return await _tents.GetAllAsync();
    }

    public Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters)
    {
        return _tents.GetPagedAsync(parameters);
    }

    public async Task<Tent> GetByIdAsync(int id)
    {
        var tent = await _tents.GetByIdAsync(id);
        if (tent is null)
        {
            throw new NotFoundException($"Tent {id} was not found.");
        }

        return tent;
    }

    public async Task<Tent> CreateAsync(Tent tent)
    {
        var label = (tent.Label ?? string.Empty).Trim();

        await EnsureSiteExistsAsync(tent.CampingSiteId);
        Validate(label, tent.Capacity, tent.PricePerNight);
        await EnsureLabelIsUniqueAsync(tent.CampingSiteId, label, excludeTentId: null);

        tent.Id = 0;
        tent.Label = label;
        tent.RowVersion = null;
        tent.CampingSite = null!;
        tent.Reservations = new List<Reservation>();

        await _tents.AddAsync(tent);
        return tent;
    }

    public async Task<Tent> UpdateAsync(int id, Tent tent)
    {
        var existing = await GetByIdAsync(id);

        var label = (tent.Label ?? string.Empty).Trim();

        await EnsureSiteExistsAsync(tent.CampingSiteId);
        Validate(label, tent.Capacity, tent.PricePerNight);
        await EnsureLabelIsUniqueAsync(tent.CampingSiteId, label, excludeTentId: id);

        existing.CampingSiteId = tent.CampingSiteId;
        existing.Label = label;
        existing.Type = tent.Type;
        existing.Capacity = tent.Capacity;
        existing.PricePerNight = tent.PricePerNight;
        existing.IsActive = tent.IsActive;

        await _tents.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        await GetByIdAsync(id);

        var reservations = await _reservations.GetAllAsync();
        if (reservations.Any(r => r.TentId == id))
        {
            throw new BusinessRuleException("Cannot delete a tent that still has reservations.");
        }

        await _tents.DeleteAsync(id);
    }

    private async Task EnsureSiteExistsAsync(int campingSiteId)
    {
        var site = await _sites.GetByIdAsync(campingSiteId);
        if (site is null)
        {
            throw new BusinessRuleException($"Camping site {campingSiteId} does not exist.");
        }
    }

    private static void Validate(string label, int capacity, decimal pricePerNight)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new BusinessRuleException("Tent label is required.");
        }

        if (capacity < 1)
        {
            throw new BusinessRuleException("Tent capacity must be at least 1.");
        }

        if (pricePerNight < 0)
        {
            throw new BusinessRuleException("Tent price per night cannot be negative.");
        }
    }

    private async Task EnsureLabelIsUniqueAsync(int campingSiteId, string label, int? excludeTentId)
    {
        var all = await _tents.GetAllAsync();
        var taken = all.Any(t =>
            t.Id != excludeTentId &&
            t.CampingSiteId == campingSiteId &&
            string.Equals(t.Label, label, StringComparison.OrdinalIgnoreCase));

        if (taken)
        {
            throw new BusinessRuleException(
                $"A tent labelled '{label}' already exists on camping site {campingSiteId}.");
        }
    }
}
