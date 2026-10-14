using System.Security.Claims;
using AutoMapper;
using Camping.Core.DTOs.Requests;
using Camping.Core.DTOs.Responses;
using Camping.Core.Enums;
using Camping.Core.Querying;
using Camping.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camping.API.Controllers;

/// <summary>
/// Guest reviews. Anyone (even without a token) can read them; any signed-in
/// user can post one, and the author is always the caller from the JWT.
/// Admins can remove a review.
/// Service-layer exceptions are translated to HTTP results centrally by the
/// exception-handling middleware (Step 12).
/// </summary>
[ApiController]
[Route("api/reviews")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _service;
    private readonly IMapper _mapper;

    public ReviewsController(IReviewService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    /// <summary>Returns a paged list of reviews, newest first. Public.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ReviewResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewResponse>>> GetAll(
        [FromQuery] ReviewQueryParameters parameters, CancellationToken cancellationToken)
    {
        var page = await _service.GetPagedAsync(parameters, cancellationToken);
        return Ok(_mapper.Map<PagedResult<ReviewResponse>>(page));
    }

    /// <summary>Returns a single review by id. Public.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        var review = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(_mapper.Map<ReviewResponse>(review));
    }

    /// <summary>Posts a review as the signed-in user.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ReviewResponse>> Create(
        [FromBody] ReviewRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(
            CurrentUserId(), request.Rating, request.Text, cancellationToken);
        var response = _mapper.Map<ReviewResponse>(created);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, response);
    }

    /// <summary>Deletes a review. Admin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>The authenticated caller's user id, from the JWT's NameIdentifier claim.</summary>
    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
