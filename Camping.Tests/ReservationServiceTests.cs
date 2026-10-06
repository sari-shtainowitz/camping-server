using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Core.Exceptions;
using Camping.Core.Interfaces;
using Camping.Service.Exceptions;
using Camping.Service.Services;
using Moq;
using Xunit;

namespace Camping.Tests;

/// <summary>
/// Step 14 - unit tests for the service layer (xUnit + Moq).
///
/// <see cref="ReservationService"/> is exercised in complete isolation: every
/// repository dependency is a strict mock, so these tests hit no database and
/// assert only the booking rules the service itself owns - a successful booking,
/// the reasons a booking is rejected, and how the service handles the limited
/// resource (the tent) under concurrent demand.
///
/// The end-to-end behaviour of the optimistic-concurrency token itself
/// (RowVersion rotation, <c>DbUpdateConcurrencyException</c>) is covered by the
/// Step 11 integration tests in <see cref="TentConcurrencyTests"/>.
/// </summary>
public class ReservationServiceTests
{
    private readonly Mock<IReservationRepository> _reservations = new(MockBehavior.Strict);
    private readonly Mock<ITentRepository> _tents = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);

    private ReservationService CreateService() =>
        new(_reservations.Object, _tents.Object, _users.Object);

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static Tent Tent(int id, bool isActive = true, decimal pricePerNight = 50m) => new()
    {
        Id = id,
        CampingSiteId = 1,
        Label = $"T-{id}",
        Type = TentType.Standard,
        Capacity = 4,
        PricePerNight = pricePerNight,
        IsActive = isActive
    };

    private static User Customer(int id) => new()
    {
        Id = id,
        FullName = "Casey Customer",
        Email = "casey@camping.test",
        Role = UserRole.Customer
    };

    private static Reservation Booking(int tentId, int userId = 1, int startInDays = 10, int nights = 3) => new()
    {
        TentId = tentId,
        UserId = userId,
        StartDate = Today.AddDays(startInDays),
        EndDate = Today.AddDays(startInDays + nights),
        Status = ReservationStatus.Pending
    };

    // Makes AddTransactionalAsync run the availability delegate the service hands it,
    // the way the real repository does inside its transaction.
    private void SetupTransactionalAddRunningTheCheck() =>
        _reservations
            .Setup(r => r.AddTransactionalAsync(It.IsAny<Reservation>(), It.IsAny<Func<Task>>()))
            .Returns((Reservation _, Func<Task> ensureTentIsFree) => ensureTentIsFree());

    // ----------------------------------------------------------------- success

    [Fact]
    public async Task CreateAsync_confirms_a_valid_booking_and_calculates_the_total_price()
    {
        _tents.Setup(t => t.GetByIdAsync(7)).ReturnsAsync(Tent(7, pricePerNight: 40m));
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(Customer(1));
        _reservations.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Reservation>());
        SetupTransactionalAddRunningTheCheck();

        var result = await CreateService().CreateAsync(Booking(tentId: 7, nights: 3));

        Assert.Equal(ReservationStatus.Confirmed, result.Status);
        Assert.Equal(120m, result.TotalPrice);   // 3 nights x 40
        Assert.Equal(0, result.Id);              // server-assigned, never taken from the request
        _reservations.Verify(
            r => r.AddTransactionalAsync(It.IsAny<Reservation>(), It.IsAny<Func<Task>>()), Times.Once);
    }

    // --------------------------------------------------------------- rejected

    [Fact]
    public async Task CreateAsync_rejects_a_booking_for_a_tent_that_does_not_exist()
    {
        _tents.Setup(t => t.GetByIdAsync(99)).ReturnsAsync((Tent?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(Booking(tentId: 99)));

        Assert.Contains("Tent 99", ex.Message);
        _reservations.Verify(
            r => r.AddTransactionalAsync(It.IsAny<Reservation>(), It.IsAny<Func<Task>>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_booking_for_a_user_that_does_not_exist()
    {
        _tents.Setup(t => t.GetByIdAsync(1)).ReturnsAsync(Tent(1));
        _users.Setup(u => u.GetByIdAsync(42)).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(Booking(tentId: 1, userId: 42)));

        Assert.Contains("User 42", ex.Message);
        _reservations.Verify(
            r => r.AddTransactionalAsync(It.IsAny<Reservation>(), It.IsAny<Func<Task>>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_booking_for_an_inactive_tent()
    {
        _tents.Setup(t => t.GetByIdAsync(5)).ReturnsAsync(Tent(5, isActive: false));
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(Customer(1));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(Booking(tentId: 5)));

        Assert.Contains("not active", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_booking_whose_end_date_is_not_after_its_start_date()
    {
        _tents.Setup(t => t.GetByIdAsync(1)).ReturnsAsync(Tent(1));
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(Customer(1));

        var zeroNights = Booking(tentId: 1);
        zeroNights.EndDate = zeroNights.StartDate;

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(zeroNights));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_booking_that_starts_in_the_past()
    {
        _tents.Setup(t => t.GetByIdAsync(1)).ReturnsAsync(Tent(1));
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(Customer(1));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(Booking(tentId: 1, startInDays: -5)));
    }

    // -------------------------------------------- limited-resource concurrency

    [Fact]
    public async Task CreateAsync_rejects_a_second_booking_that_overlaps_an_existing_one_for_the_same_tent()
    {
        _tents.Setup(t => t.GetByIdAsync(3)).ReturnsAsync(Tent(3));
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(Customer(1));

        var existing = Booking(tentId: 3, startInDays: 10, nights: 4);
        existing.Id = 1;
        existing.Status = ReservationStatus.Confirmed;
        _reservations.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Reservation> { existing });
        SetupTransactionalAddRunningTheCheck();

        var overlapping = Booking(tentId: 3, startInDays: 12, nights: 3);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(overlapping));

        Assert.Contains("already reserved", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_allows_a_booking_for_the_same_tent_on_non_overlapping_dates()
    {
        _tents.Setup(t => t.GetByIdAsync(3)).ReturnsAsync(Tent(3, pricePerNight: 30m));
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(Customer(1));

        var existing = Booking(tentId: 3, startInDays: 10, nights: 3);   // [+10 .. +13)
        existing.Id = 1;
        existing.Status = ReservationStatus.Confirmed;
        _reservations.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Reservation> { existing });
        SetupTransactionalAddRunningTheCheck();

        var later = Booking(tentId: 3, startInDays: 13, nights: 2);      // [+13 .. +15) - touches but does not overlap

        var result = await CreateService().CreateAsync(later);

        Assert.Equal(ReservationStatus.Confirmed, result.Status);
        Assert.Equal(60m, result.TotalPrice);
    }

    [Fact]
    public async Task CreateAsync_propagates_a_ConcurrencyConflictException_when_a_racing_request_took_the_tent()
    {
        _tents.Setup(t => t.GetByIdAsync(8)).ReturnsAsync(Tent(8));
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(Customer(1));

        // The availability check passes here, but the transactional write loses the
        // optimistic-concurrency race on the tent row in the data layer.
        _reservations
            .Setup(r => r.AddTransactionalAsync(It.IsAny<Reservation>(), It.IsAny<Func<Task>>()))
            .ThrowsAsync(new ConcurrencyConflictException(
                "That tent was just booked by someone else. Please check availability and try again."));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => CreateService().CreateAsync(Booking(tentId: 8)));
    }

    [Fact]
    public async Task CreateAsync_defers_the_availability_check_into_the_transactional_write()
    {
        _tents.Setup(t => t.GetByIdAsync(2)).ReturnsAsync(Tent(2));
        _users.Setup(u => u.GetByIdAsync(1)).ReturnsAsync(Customer(1));

        var availabilityChecks = 0;
        _reservations.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Reservation>())
            .Callback(() => availabilityChecks++);

        Func<Task>? capturedCheck = null;
        _reservations
            .Setup(r => r.AddTransactionalAsync(It.IsAny<Reservation>(), It.IsAny<Func<Task>>()))
            .Callback((Reservation _, Func<Task> check) => capturedCheck = check)
            .Returns(Task.CompletedTask);

        await CreateService().CreateAsync(Booking(tentId: 2));

        // The service handed the repository a re-check delegate and did NOT run it
        // itself - the repository runs it inside the transaction that guards the tent.
        Assert.NotNull(capturedCheck);
        Assert.Equal(0, availabilityChecks);

        await capturedCheck!();
        Assert.Equal(1, availabilityChecks);
    }
}
