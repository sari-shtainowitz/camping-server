using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;
using Camping.Service.Interfaces;

namespace Camping.Service.Services;

/// <summary>
/// Business rules for amenities:
///   * the name is required and must be unique (case-insensitive).
/// </summary>
public class AmenityService : IAmenityService
{
    private readonly IAmenityRepository _amenities;

    public AmenityService(IAmenityRepository amenities)
    {
        _amenities = amenities;
    }

    public async Task<IReadOnlyList<Amenity>> GetAllAsync()
    {
        return await _amenities.GetAllAsync();
    }

    public Task<PagedResult<Amenity>> GetPagedAsync(AmenityQueryParameters parameters)
    {
        return _amenities.GetPagedAsync(parameters);
    }

    public async Task<Amenity> GetByIdAsync(int id)
    {
        var amenity = await _amenities.GetByIdAsync(id);
        if (amenity is null)
        {
            throw new NotFoundException($"Amenity {id} was not found.");
        }

        return amenity;
    }

    public async Task<Amenity> CreateAsync(Amenity amenity)
    {
        var name = (amenity.Name ?? string.Empty).Trim();

        Validate(name);
        await EnsureNameIsUniqueAsync(name, excludeAmenityId: null);

        amenity.Id = 0;
        amenity.Name = name;
        amenity.Description = string.IsNullOrWhiteSpace(amenity.Description)
            ? null
            : amenity.Description.Trim();
        amenity.CampingSites = new List<CampingSite>();

        await _amenities.AddAsync(amenity);
        return amenity;
    }

    public async Task<Amenity> UpdateAsync(int id, Amenity amenity)
    {
        var existing = await GetByIdAsync(id);

        var name = (amenity.Name ?? string.Empty).Trim();

        Validate(name);
        await EnsureNameIsUniqueAsync(name, excludeAmenityId: id);

        existing.Name = name;
        existing.Description = string.IsNullOrWhiteSpace(amenity.Description)
            ? null
            : amenity.Description.Trim();

        await _amenities.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        await GetByIdAsync(id);
        await _amenities.DeleteAsync(id);
    }

    private static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("Amenity name is required.");
        }
    }

    private async Task EnsureNameIsUniqueAsync(string name, int? excludeAmenityId)
    {
        var all = await _amenities.GetAllAsync();
        var taken = all.Any(a =>
            a.Id != excludeAmenityId &&
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        if (taken)
        {
            throw new BusinessRuleException($"An amenity named '{name}' already exists.");
        }
    }
}
