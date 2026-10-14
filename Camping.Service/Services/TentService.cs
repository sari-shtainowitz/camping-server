using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;

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

    public Task<PagedResult<Tent>> GetPagedAsync(TentQueryParameters parameters, CancellationToken cancellationToken)
    {
        return _tents.GetPagedAsync(parameters, cancellationToken);
    }

    public async Task<Tent> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var tent = await _tents.GetByIdAsync(id, cancellationToken);
        if (tent is null)
        {
            throw new NotFoundException($"Tent {id} was not found.");
        }

        return tent;
    }

    public async Task<Tent> CreateAsync(Tent tent, CancellationToken cancellationToken)
    {
        var label = (tent.Label ?? string.Empty).Trim();

        await EnsureSiteExistsAsync(tent.CampingSiteId, cancellationToken);
        Validate(label, tent.Capacity, tent.PricePerNight);
        await EnsureLabelIsUniqueAsync(tent.CampingSiteId, label, excludeTentId: null, cancellationToken);

        tent.Id = 0;
        tent.Label = label;
        tent.RowVersion = null;
        tent.CampingSite = null!;
        tent.Reservations = new List<Reservation>();

        await _tents.AddAsync(tent, cancellationToken);
        return tent;
    }

    public async Task<Tent> UpdateAsync(int id, Tent tent, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(id, cancellationToken);

        var label = (tent.Label ?? string.Empty).Trim();

        await EnsureSiteExistsAsync(tent.CampingSiteId, cancellationToken);
        Validate(label, tent.Capacity, tent.PricePerNight);
        await EnsureLabelIsUniqueAsync(tent.CampingSiteId, label, excludeTentId: id, cancellationToken);

        existing.CampingSiteId = tent.CampingSiteId;
        existing.Label = label;
        existing.Type = tent.Type;
        existing.Capacity = tent.Capacity;
        existing.PricePerNight = tent.PricePerNight;
        existing.IsActive = tent.IsActive;

        await _tents.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await GetByIdAsync(id, cancellationToken);

        if (await _reservations.ExistsForTentAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("Cannot delete a tent that still has reservations.");
        }

        await _tents.DeleteAsync(id, cancellationToken);
    }

    private async Task EnsureSiteExistsAsync(int campingSiteId, CancellationToken cancellationToken)
    {
        var site = await _sites.GetByIdAsync(campingSiteId, cancellationToken);
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

    private async Task EnsureLabelIsUniqueAsync(
        int campingSiteId, string label, int? excludeTentId, CancellationToken cancellationToken)
    {
        if (await _tents.LabelExistsAsync(campingSiteId, label, excludeTentId, cancellationToken))
        {
            throw new BusinessRuleException(
                $"A tent labelled '{label}' already exists on camping site {campingSiteId}.");
        }
    }
}
