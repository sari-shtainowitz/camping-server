using System.Security.Claims;
using AutoMapper;
using Camping.API.Controllers;
using Camping.API.DTOs.Requests;
using Camping.API.DTOs.Responses;
using Camping.API.Mapping;
using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Core.Querying;
using Camping.Service.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Camping.Tests;

/// <summary>
/// Server-side authorization tests for <see cref="ReservationsController"/>.
///
/// These instantiate the real, production <see cref="ReservationsController"/>
/// - the exact class ASP.NET Core invokes for a request - with a
/// <see cref="ClaimsPrincipal"/> carrying the same claim types
/// <c>JwtTokenService</c> puts in a real token (<see cref="ClaimTypes.NameIdentifier"/>,
/// <see cref="ClaimTypes.Role"/>) attached to its <see cref="ControllerBase.ControllerContext"/>,
/// exactly as ASP.NET Core's authentication middleware would after validating a
/// JWT. Only <see cref="IReservationService"/> is mocked (the same boundary
/// <see cref="ReservationServiceTests"/> isolates at); <see cref="IMapper"/> is
/// the real, production <see cref="MappingProfile"/>. This exercises the
/// controller's own <c>IsAdmin()</c> / <c>CurrentUserId()</c> / <c>Forbid()</c>
/// ownership checks directly - it does not reimplement or duplicate them.
/// </summary>
public class ReservationsControllerAuthorizationTests
{
    private readonly Mock<IReservationService> _service = new(MockBehavior.Strict);

    private static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();

    private ReservationsController CreateController(int userId, UserRole role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        }, authenticationType: "TestAuth");

        return new ReservationsController(_service.Object, Mapper)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private ReservationsController AsCustomer(int userId) => CreateController(userId, UserRole.Customer);

    private ReservationsController AsAdmin(int userId) => CreateController(userId, UserRole.Admin);

    private static Reservation Reservation(int id, int userId) => new()
    {
        Id = id,
        TentId = 1,
        UserId = userId,
        StartDate = new DateOnly(2099, 1, 1),
        EndDate = new DateOnly(2099, 1, 4),
        Status = ReservationStatus.Confirmed,
        TotalPrice = 120m,
        CreatedAt = DateTime.UtcNow
    };

    private static ReservationRequest SampleRequest() => new()
    {
        TentId = 1,
        UserId = 99,
        StartDate = new DateOnly(2099, 2, 1),
        EndDate = new DateOnly(2099, 2, 4),
        Status = ReservationStatus.Confirmed
    };

    // --------------------------------------------------------- GetById (1, 2, 6)

    [Fact]
    public async Task GetById_allows_a_customer_to_read_their_own_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(5)).ReturnsAsync(Reservation(id: 5, userId: 42));

        var result = await AsCustomer(userId: 42).GetById(5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ReservationResponse>(ok.Value);
        Assert.Equal(5, body.Id);
        Assert.Equal(42, body.UserId);
    }

    [Fact]
    public async Task GetById_returns_403_when_a_customer_requests_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9)).ReturnsAsync(Reservation(id: 9, userId: 99));

        var result = await AsCustomer(userId: 42).GetById(9);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetById_allows_an_admin_to_read_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9)).ReturnsAsync(Reservation(id: 9, userId: 99));

        var result = await AsAdmin(userId: 1).GetById(9);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ReservationResponse>(ok.Value);
        Assert.Equal(99, body.UserId);
    }

    // ------------------------------------------------------------------ Update (3)

    [Fact]
    public async Task Update_returns_403_when_a_customer_updates_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9)).ReturnsAsync(Reservation(id: 9, userId: 99));

        var result = await AsCustomer(userId: 42).Update(9, SampleRequest());

        Assert.IsType<ForbidResult>(result.Result);
        _service.Verify(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<Reservation>()), Times.Never);
    }

    // ------------------------------------------------------------------ Delete (4, 7)

    [Fact]
    public async Task Delete_returns_403_when_a_customer_deletes_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9)).ReturnsAsync(Reservation(id: 9, userId: 99));

        var result = await AsCustomer(userId: 42).Delete(9);

        Assert.IsType<ForbidResult>(result);
        _service.Verify(s => s.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Delete_allows_an_admin_to_delete_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9)).ReturnsAsync(Reservation(id: 9, userId: 99));
        _service.Setup(s => s.DeleteAsync(9)).Returns(Task.CompletedTask);

        var result = await AsAdmin(userId: 1).Delete(9);

        Assert.IsType<NoContentResult>(result);
        _service.Verify(s => s.DeleteAsync(9), Times.Once);
    }

    // ------------------------------------------------------------------- GetAll (5)

    [Fact]
    public async Task GetAll_ignores_a_customers_attempt_to_list_another_users_reservations()
    {
        ReservationQueryParameters? received = null;
        _service.Setup(s => s.GetPagedAsync(It.IsAny<ReservationQueryParameters>()))
            .Callback<ReservationQueryParameters>(p => received = p)
            .ReturnsAsync(new PagedResult<Reservation>
            {
                Items = Array.Empty<Reservation>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            });

        // The caller is Customer 42 but asks the endpoint for Customer 999's reservations.
        var requestedParameters = new ReservationQueryParameters { UserId = 999 };

        await AsCustomer(userId: 42).GetAll(requestedParameters);

        Assert.NotNull(received);
        Assert.Equal(42, received!.UserId);
    }
}
