using System.Security.Claims;
using AutoMapper;
using Camping.Core.DTOs.Requests;
using Camping.Core.DTOs.Responses;
using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Core.Querying;
using Camping.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camping.API.Controllers;

/// <summary>
/// Reservations. All endpoints require an authenticated user; the booking rules
/// (availability, pricing, date checks) live in <see cref="IReservationService"/>.
/// Service-layer exceptions are translated to HTTP results centrally by the
/// exception-handling middleware (Step 12) - including the 409 Conflict for a
/// tent that is already booked or was taken by a racing request.
///
/// Ownership is enforced here, on the server, from the JWT: a Customer always
/// books for themselves (any userId in the body is ignored) and may only read,
/// update or delete their own reservations; an Admin may act on any reservation.
/// </summary>
[ApiController]
[Route("api/reservations")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _service;
    private readonly IMapper _mapper;

    public ReservationsController(IReservationService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    /// <summary>
    /// Returns a paged, optionally filtered and sorted list of reservations.
    /// A Customer only ever sees their own reservations - any <c>userId</c> filter
    /// they supply is overridden with their own id; an Admin sees everyone's.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReservationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReservationResponse>>> GetAll(
        [FromQuery] ReservationQueryParameters parameters, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            parameters.UserId = CurrentUserId();
        }

        var page = await _service.GetPagedAsync(parameters, cancellationToken);
        return Ok(_mapper.Map<PagedResult<ReservationResponse>>(page));
    }

    /// <summary>Returns a single reservation by id. A Customer may only read their own.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> GetById(
        [FromRoute] int id, CancellationToken cancellationToken)
    {
        var reservation = await _service.GetByIdAsync(id, cancellationToken);
        if (!IsAdmin() && reservation.UserId != CurrentUserId())
        {
            return Forbid();
        }

        return Ok(_mapper.Map<ReservationResponse>(reservation));
    }

    /// <summary>
    /// Creates a reservation for the caller. Booking is a Customer action; Admins
    /// manage the catalogue and view the schedule but never book a tent.
    /// The guest is always the authenticated caller - a userId in the body is ignored.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Customer))]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Create(
        [FromBody] ReservationRequest request, CancellationToken cancellationToken)
    {
        var reservation = _mapper.Map<Reservation>(request);
        reservation.UserId = CurrentUserId();

        var created = await _service.CreateAsync(reservation, cancellationToken);
        var response = _mapper.Map<ReservationResponse>(created);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, response);
    }

    /// <summary>
    /// Updates a reservation. A Customer may only update their own and can never
    /// move it to another user; an Admin may also reassign it via <c>userId</c>.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Update(
        [FromRoute] int id, [FromBody] ReservationRequest request, CancellationToken cancellationToken)
    {
        var existing = await _service.GetByIdAsync(id, cancellationToken);
        if (!IsAdmin() && existing.UserId != CurrentUserId())
        {
            return Forbid();
        }

        var reservation = _mapper.Map<Reservation>(request);
        reservation.UserId = IsAdmin()
            ? request.UserId ?? existing.UserId
            : existing.UserId;

        var updated = await _service.UpdateAsync(id, reservation, cancellationToken);
        return Ok(_mapper.Map<ReservationResponse>(updated));
    }

    /// <summary>Deletes a reservation. A Customer may only delete their own.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var existing = await _service.GetByIdAsync(id, cancellationToken);
        if (!IsAdmin() && existing.UserId != CurrentUserId())
        {
            return Forbid();
        }

        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>The authenticated caller's user id, from the JWT's NameIdentifier claim.</summary>
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Whether the caller holds the Admin role.</summary>
    private bool IsAdmin() => User.IsInRole(nameof(UserRole.Admin));
}
