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
/// Camping sites catalogue. Reading requires any authenticated user; creating,
/// updating and deleting require the Admin role.
/// Service-layer exceptions are translated to HTTP results centrally by the
/// exception-handling middleware (Step 12).
/// </summary>
[ApiController]
[Route("api/camping-sites")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class CampingSitesController : ControllerBase
{
    private readonly ICampingSiteService _service;
    private readonly IMapper _mapper;

    public CampingSitesController(ICampingSiteService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    /// <summary>Returns a paged, optionally filtered and sorted list of camping sites.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CampingSiteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CampingSiteResponse>>> GetAll(
        [FromQuery] CampingSiteQueryParameters parameters)
    {
        var page = await _service.GetPagedAsync(parameters);
        return Ok(_mapper.Map<PagedResult<CampingSiteResponse>>(page));
    }

    /// <summary>Returns a single camping site by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CampingSiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampingSiteResponse>> GetById([FromRoute] int id)
    {
        var site = await _service.GetByIdAsync(id);
        return Ok(_mapper.Map<CampingSiteResponse>(site));
    }

    /// <summary>Creates a camping site. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(CampingSiteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CampingSiteResponse>> Create([FromBody] CampingSiteRequest request)
    {
        var created = await _service.CreateAsync(_mapper.Map<CampingSite>(request));
        var response = _mapper.Map<CampingSiteResponse>(created);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, response);
    }

    /// <summary>Updates a camping site. Admin only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(CampingSiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampingSiteResponse>> Update([FromRoute] int id, [FromBody] CampingSiteRequest request)
    {
        var updated = await _service.UpdateAsync(id, _mapper.Map<CampingSite>(request));
        return Ok(_mapper.Map<CampingSiteResponse>(updated));
    }

    /// <summary>Deletes a camping site. Admin only.</summary>
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
