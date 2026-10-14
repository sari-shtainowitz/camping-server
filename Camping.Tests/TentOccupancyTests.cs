using Camping.Core.Entities;
using Camping.Core.Interfaces;
using Camping.Core.Querying;
using Camping.Service.Exceptions;
using Camping.Service.Services;
using Moq;
using Xunit;

namespace Camping.Tests;

/// <summary>
/// The public occupancy view behind GET /api/tents/occupancy: every guest can
/// see which tents are booked when, so a tent one guest booked shows as taken
/// for everybody (TentService.GetOccupancyAsync, repositories mocked).
/// </summary>
public class TentOccupancyTests
{
    private readonly Mock<ITentRepository> _tents = new(MockBehavior.Strict);
    private readonly Mock<ICampingSiteRepository> _sites = new(MockBehavior.Strict);
    private readonly Mock<IReservationRepository> _reservations = new(MockBehavior.Strict);

    private TentService CreateService() => new(_tents.Object, _sites.Object, _reservations.Object);

    [Fact]
    public async Task GetOccupancyAsync_returns_the_bookings_overlapping_the_requested_window()
    {
        var from = new DateOnly(2099, 3, 1);
        var to = new DateOnly(2099, 3, 8);
        var booked = new List<Reservation>
        {
            new() { TentId = 1, StartDate = new DateOnly(2099, 3, 2), EndDate = new DateOnly(2099, 3, 5) }
        };
        _reservations.Setup(r => r.GetOccupancyAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booked);

        var result = await CreateService().GetOccupancyAsync(
            new OccupancyQueryParameters { From = new DateTime(2099, 3, 1), To = new DateTime(2099, 3, 8) },
            CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal(1, only.TentId);
    }

    [Fact]
    public async Task GetOccupancyAsync_defaults_to_today_until_one_year_ahead()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _reservations.Setup(r => r.GetOccupancyAsync(today, today.AddYears(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reservation>());

        var result = await CreateService().GetOccupancyAsync(new OccupancyQueryParameters(), CancellationToken.None);

        Assert.Empty(result);
        _reservations.VerifyAll();
    }

    [Fact]
    public async Task GetOccupancyAsync_rejects_a_window_that_ends_before_it_starts()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().GetOccupancyAsync(
            new OccupancyQueryParameters { From = new DateTime(2099, 3, 8), To = new DateTime(2099, 3, 1) },
            CancellationToken.None));
    }
}
