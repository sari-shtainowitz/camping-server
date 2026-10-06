using System.Security.Claims;
using Camping.API.Authentication;
using Camping.API.DTOs.Requests;
using Camping.API.DTOs.Responses;
using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camping.API.Controllers;

/// <summary>
/// Authentication endpoints: registration, login, and two small protected
/// endpoints that demonstrate <c>[Authorize]</c> and role-based authorization.
/// Business validation lives in <see cref="IAuthService"/>; its exceptions are
/// translated to HTTP responses centrally by the exception-handling middleware
/// (Step 12) - a failed registration is 400, a failed login is 401.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtTokenService _tokenService;

    public AuthController(IAuthService authService, IJwtTokenService tokenService)
    {
        _authService = authService;
        _tokenService = tokenService;
    }

    /// <summary>Registers a new Customer account and returns a token.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var input = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber
        };

        var user = await _authService.RegisterAsync(input, request.Password);
        return CreatedAtAction(nameof(Me), BuildResponse(user));
    }

    /// <summary>Exchanges email + password for a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _authService.AuthenticateAsync(request.Email, request.Password);
        return Ok(BuildResponse(user));
    }

    /// <summary>Returns the claims of the caller. Requires any valid token.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue(ClaimTypes.Email),
            fullName = User.Identity?.Name,
            role = User.FindFirstValue(ClaimTypes.Role)
        });
    }

    /// <summary>Only reachable with a token whose role is Admin.</summary>
    [HttpGet("admin-area")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public ActionResult<object> AdminArea()
    {
        return Ok(new { message = "You have admin access." });
    }

    private AuthResponse BuildResponse(User user)
    {
        var token = _tokenService.CreateToken(user);
        return new AuthResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString()
        };
    }
}
