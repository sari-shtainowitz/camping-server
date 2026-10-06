using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;
using Camping.Service.Interfaces;

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

    public async Task<IReadOnlyList<CampingSite>> GetAllAsync()
    {
        return await _sites.GetAllAsync();
    }

    public Task<PagedResult<CampingSite>> GetPagedAsync(CampingSiteQueryParameters parameters)
    {
        return _sites.GetPagedAsync(parameters);
    }

    public async Task<CampingSite> GetByIdAsync(int id)
    {
        var site = await _sites.GetByIdAsync(id);
        if (site is null)
        {
            throw new NotFoundException($"Camping site {id} was not found.");
        }

        return site;
    }

    public async Task<CampingSite> CreateAsync(CampingSite campingSite)
    {
        var name = (campingSite.Name ?? string.Empty).Trim();
        var address = (campingSite.Address ?? string.Empty).Trim();

        Validate(name, address);
        await EnsureNameIsUniqueAsync(name, excludeSiteId: null);

        campingSite.Id = 0;
        campingSite.Name = name;
        campingSite.Address = address;
        campingSite.Description = NormalizeOptional(campingSite.Description);
        campingSite.CreatedAt = DateTime.UtcNow;
        campingSite.Tents = new List<Tent>();
        campingSite.Amenities = new List<Amenity>();

        await _sites.AddAsync(campingSite);
        return campingSite;
    }

    public async Task<CampingSite> UpdateAsync(int id, CampingSite campingSite)
    {
        var existing = await GetByIdAsync(id);

        var name = (campingSite.Name ?? string.Empty).Trim();
        var address = (campingSite.Address ?? string.Empty).Trim();

        Validate(name, address);
        await EnsureNameIsUniqueAsync(name, excludeSiteId: id);

        existing.Name = name;
        existing.Address = address;
        existing.Description = NormalizeOptional(campingSite.Description);

        await _sites.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _sites.GetByIdWithTentsAsync(id);
        if (existing is null)
        {
            throw new NotFoundException($"Camping site {id} was not found.");
        }

        if (existing.Tents.Any())
        {
            throw new BusinessRuleException("Cannot delete a camping site that still has tents.");
        }

        await _sites.DeleteAsync(id);
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

    private async Task EnsureNameIsUniqueAsync(string name, int? excludeSiteId)
    {
        var all = await _sites.GetAllAsync();
        var taken = all.Any(s =>
            s.Id != excludeSiteId &&
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        if (taken)
        {
            throw new BusinessRuleException($"A camping site named '{name}' already exists.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
