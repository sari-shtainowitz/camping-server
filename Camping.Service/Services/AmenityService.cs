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

    public Task<PagedResult<Amenity>> GetPagedAsync(
        AmenityQueryParameters parameters, CancellationToken cancellationToken)
    {
        return _amenities.GetPagedAsync(parameters, cancellationToken);
    }

    public async Task<Amenity> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var amenity = await _amenities.GetByIdAsync(id, cancellationToken);
        if (amenity is null)
        {
            throw new NotFoundException($"Amenity {id} was not found.");
        }

        return amenity;
    }

    public async Task<Amenity> CreateAsync(Amenity amenity, CancellationToken cancellationToken)
    {
        var name = (amenity.Name ?? string.Empty).Trim();

        Validate(name);
        await EnsureNameIsUniqueAsync(name, excludeAmenityId: null, cancellationToken);

        amenity.Id = 0;
        amenity.Name = name;
        amenity.Description = string.IsNullOrWhiteSpace(amenity.Description)
            ? null
            : amenity.Description.Trim();
        amenity.CampingSites = new List<CampingSite>();

        await _amenities.AddAsync(amenity, cancellationToken);
        return amenity;
    }

    public async Task<Amenity> UpdateAsync(int id, Amenity amenity, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(id, cancellationToken);

        var name = (amenity.Name ?? string.Empty).Trim();

        Validate(name);
        await EnsureNameIsUniqueAsync(name, excludeAmenityId: id, cancellationToken);

        existing.Name = name;
        existing.Description = string.IsNullOrWhiteSpace(amenity.Description)
            ? null
            : amenity.Description.Trim();

        await _amenities.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await GetByIdAsync(id, cancellationToken);
        await _amenities.DeleteAsync(id, cancellationToken);
    }

    private static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("Amenity name is required.");
        }
    }

    private async Task EnsureNameIsUniqueAsync(string name, int? excludeAmenityId, CancellationToken cancellationToken)
    {
        if (await _amenities.NameExistsAsync(name, excludeAmenityId, cancellationToken))
        {
            throw new BusinessRuleException($"An amenity named '{name}' already exists.");
        }
    }
}
