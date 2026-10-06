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
/// Tents catalogue. Reading requires any authenticated user; creating, updating
/// and deleting require the Admin role.
/// Service-layer exceptions are translated to HTTP results centrally by the
/// exception-handling middleware (Step 12).
/// </summary>
[ApiController]
[Route("api/tents")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class TentsController : ControllerBase
{
    private readonly ITentService _service;
    private readonly IMapper _mapper;

    public TentsController(ITentService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    /// <summary>Returns a paged, optionally filtered and sorted list of tents.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TentResponse>>> GetAll(
        [FromQuery] TentQueryParameters parameters)
    {
        var page = await _service.GetPagedAsync(parameters);
        return Ok(_mapper.Map<PagedResult<TentResponse>>(page));
    }

    /// <summary>Returns a single tent by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TentResponse>> GetById([FromRoute] int id)
    {
        var tent = await _service.GetByIdAsync(id);
        return Ok(_mapper.Map<TentResponse>(tent));
    }

    /// <summary>Creates a tent. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(TentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TentResponse>> Create([FromBody] TentRequest request)
    {
        var created = await _service.CreateAsync(_mapper.Map<Tent>(request));
        var response = _mapper.Map<TentResponse>(created);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, response);
    }

    /// <summary>Updates a tent. Admin only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(TentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TentResponse>> Update([FromRoute] int id, [FromBody] TentRequest request)
    {
        var updated = await _service.UpdateAsync(id, _mapper.Map<Tent>(request));
        return Ok(_mapper.Map<TentResponse>(updated));
    }

    /// <summary>Deletes a tent. Admin only.</summary>
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
