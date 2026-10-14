using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for camping sites:
///   * name and address are required;
///   * name must be unique (case-insensitive);
///   * a site that still owns tents cannot be deleted.
/// </summary>
public class CampingSiteService : ICampingSiteService
{
    private readonly ICampingSiteRepository _sites;

    public CampingSiteService(ICampingSiteRepository sites)
    {
        _sites = sites;
    }

    public Task<PagedResult<CampingSite>> GetPagedAsync(
        CampingSiteQueryParameters parameters, CancellationToken cancellationToken)
    {
        return _sites.GetPagedAsync(parameters, cancellationToken);
    }

    // For reading: the site together with its amenities.
    public async Task<CampingSite> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var site = await _sites.GetWithAmenitiesAsync(id, cancellationToken);
        if (site is null)
        {
            throw new NotFoundException($"Camping site {id} was not found.");
        }

        return site;
    }

    // For writing: the plain row, so saving never touches the amenity links.
    private async Task<CampingSite> RequireSiteAsync(int id, CancellationToken cancellationToken)
    {
        var site = await _sites.GetByIdAsync(id, cancellationToken);
        if (site is null)
        {
            throw new NotFoundException($"Camping site {id} was not found.");
        }

        return site;
    }

    public async Task<CampingSite> CreateAsync(CampingSite campingSite, CancellationToken cancellationToken)
    {
        var name = (campingSite.Name ?? string.Empty).Trim();
        var address = (campingSite.Address ?? string.Empty).Trim();

        Validate(name, address);
        await EnsureNameIsUniqueAsync(name, excludeSiteId: null, cancellationToken);

        campingSite.Id = 0;
        campingSite.Name = name;
        campingSite.Address = address;
        campingSite.Description = NormalizeOptional(campingSite.Description);
        campingSite.CreatedAt = DateTime.UtcNow;
        campingSite.Tents = new List<Tent>();
        campingSite.Amenities = new List<Amenity>();

        await _sites.AddAsync(campingSite, cancellationToken);
        return campingSite;
    }

    public async Task<CampingSite> UpdateAsync(int id, CampingSite campingSite, CancellationToken cancellationToken)
    {
        var existing = await RequireSiteAsync(id, cancellationToken);

        var name = (campingSite.Name ?? string.Empty).Trim();
        var address = (campingSite.Address ?? string.Empty).Trim();

        Validate(name, address);
        await EnsureNameIsUniqueAsync(name, excludeSiteId: id, cancellationToken);

        existing.Name = name;
        existing.Address = address;
        existing.Description = NormalizeOptional(campingSite.Description);

        await _sites.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await RequireSiteAsync(id, cancellationToken);

        if (await _sites.HasTentsAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("Cannot delete a camping site that still has tents.");
        }

        await _sites.DeleteAsync(id, cancellationToken);
    }

    private static void Validate(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("Camping site name is required.");
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new BusinessRuleException("Camping site address is required.");
        }
    }

    private async Task EnsureNameIsUniqueAsync(string name, int? excludeSiteId, CancellationToken cancellationToken)
    {
        if (await _sites.NameExistsAsync(name, excludeSiteId, cancellationToken))
        {
            throw new BusinessRuleException($"A camping site named '{name}' already exists.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
