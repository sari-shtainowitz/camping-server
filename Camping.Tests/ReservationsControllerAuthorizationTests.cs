using System.Security.Claims;
using AutoMapper;
using Camping.API.Controllers;
using Camping.Core.DTOs.Requests;
using Camping.Core.DTOs.Responses;
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
        _service.Setup(s => s.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation(id: 5, userId: 42));

        var result = await AsCustomer(userId: 42).GetById(5, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ReservationResponse>(ok.Value);
        Assert.Equal(5, body.Id);
        Assert.Equal(42, body.UserId);
    }

    [Fact]
    public async Task GetById_returns_403_when_a_customer_requests_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation(id: 9, userId: 99));

        var result = await AsCustomer(userId: 42).GetById(9, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetById_allows_an_admin_to_read_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation(id: 9, userId: 99));

        var result = await AsAdmin(userId: 1).GetById(9, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ReservationResponse>(ok.Value);
        Assert.Equal(99, body.UserId);
    }

    // ------------------------------------------------------------------ Create (8)

    [Fact]
    public async Task Create_books_for_the_caller_in_the_JWT_and_ignores_a_userId_in_the_body()
    {
        Reservation? received = null;
        _service.Setup(s => s.CreateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .Callback<Reservation, CancellationToken>((r, _) => received = r)
            .ReturnsAsync((Reservation r, CancellationToken _) => { r.Id = 77; return r; });

        // Customer 42 tries to book in the name of user 99.
        var request = SampleRequest();
        Assert.Equal(99, request.UserId);

        var result = await AsCustomer(userId: 42).Create(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.NotNull(received);
        Assert.Equal(42, received!.UserId);
    }

    [Fact]
    public async Task Update_by_a_customer_cannot_move_their_reservation_to_another_user()
    {
        _service.Setup(s => s.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Reservation(id: 5, userId: 42));
        Reservation? received = null;
        _service.Setup(s => s.UpdateAsync(5, It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .Callback<int, Reservation, CancellationToken>((_, r, _) => received = r)
            .ReturnsAsync((int _, Reservation r, CancellationToken _) => r);

        await AsCustomer(userId: 42).Update(5, SampleRequest(), CancellationToken.None);   // body says user 99

        Assert.NotNull(received);
        Assert.Equal(42, received!.UserId);
    }

    // ------------------------------------------------------------------ Update (3)

    [Fact]
    public async Task Update_returns_403_when_a_customer_updates_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation(id: 9, userId: 99));

        var result = await AsCustomer(userId: 42).Update(9, SampleRequest(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        _service.Verify(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------ Delete (4, 7)

    [Fact]
    public async Task Delete_returns_403_when_a_customer_deletes_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation(id: 9, userId: 99));

        var result = await AsCustomer(userId: 42).Delete(9, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        _service.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_allows_an_admin_to_delete_another_users_reservation()
    {
        _service.Setup(s => s.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation(id: 9, userId: 99));
        _service.Setup(s => s.DeleteAsync(9, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await AsAdmin(userId: 1).Delete(9, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _service.Verify(s => s.DeleteAsync(9, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------- GetAll (5)

    [Fact]
    public async Task GetAll_ignores_a_customers_attempt_to_list_another_users_reservations()
    {
        ReservationQueryParameters? received = null;
        _service.Setup(s => s.GetPagedAsync(It.IsAny<ReservationQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<ReservationQueryParameters, CancellationToken>((p, _) => received = p)
            .ReturnsAsync(new PagedResult<Reservation>
            {
                Items = Array.Empty<Reservation>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            });

        // The caller is Customer 42 but asks the endpoint for Customer 999's reservations.
        var requestedParameters = new ReservationQueryParameters { UserId = 999 };

        await AsCustomer(userId: 42).GetAll(requestedParameters, CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(42, received!.UserId);
    }
}
