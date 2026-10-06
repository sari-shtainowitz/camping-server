using AutoMapper;
using Camping.API.DTOs.Requests;
using Camping.API.DTOs.Responses;
using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Core.Querying;
using Camping.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camping.API.Controllers;

/// <summary>
/// Amenities catalogue. Reading requires any authenticated user; creating,
/// updating and deleting require the Admin role.
/// Service-layer exceptions are translated to HTTP results centrally by the
/// exception-handling middleware (Step 12).
/// </summary>
[ApiController]
[Route("api/amenities")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class AmenitiesController : ControllerBase
{
    private readonly IAmenityService _service;
    private readonly IMapper _mapper;

    public AmenitiesController(IAmenityService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    /// <summary>Returns a paged, optionally filtered and sorted list of amenities.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AmenityResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AmenityResponse>>> GetAll(
        [FromQuery] AmenityQueryParameters parameters)
    {
        var page = await _service.GetPagedAsync(parameters);
        return Ok(_mapper.Map<PagedResult<AmenityResponse>>(page));
    }

    /// <summary>Returns a single amenity by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AmenityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AmenityResponse>> GetById([FromRoute] int id)
    {
        var amenity = await _service.GetByIdAsync(id);
        return Ok(_mapper.Map<AmenityResponse>(amenity));
    }

    /// <summary>Creates an amenity. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(AmenityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AmenityResponse>> Create([FromBody] AmenityRequest request)
    {
        var created = await _service.CreateAsync(_mapper.Map<Amenity>(request));
        var response = _mapper.Map<AmenityResponse>(created);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, response);
    }

    /// <summary>Updates an amenity. Admin only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(AmenityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AmenityResponse>> Update([FromRoute] int id, [FromBody] AmenityRequest request)
    {
        var updated = await _service.UpdateAsync(id, _mapper.Map<Amenity>(request));
        return Ok(_mapper.Map<AmenityResponse>(updated));
    }

    /// <summary>Deletes an amenity. Admin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
