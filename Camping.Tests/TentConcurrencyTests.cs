using Camping.Core.Entities;
using Camping.Core.Enums;
using Camping.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CampingDbContext = Camping.Data.DbContext;

namespace Camping.Tests;

/// <summary>
/// Step 11 - optimistic concurrency on the limited resource (the tent).
///
/// Both tests use two completely separate <see cref="CampingDbContext"/>
/// instances against one real SQL Server database (see
/// <see cref="SqlServerTestDatabase"/>). The token rotation lives in
/// <c>DbContext.SaveChanges</c> and is not altered for the test.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public class TentConcurrencyTests
{
    private readonly SqlServerTestDatabase _database;

    public TentConcurrencyTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task Second_context_saving_its_stale_tent_throws_DbUpdateConcurrencyException()
    {
        _database.RequireAvailable();

        const int tentId = 1;
        const int userId = 1;

        await using var contextA = _database.NewContext();
        await using var contextB = _database.NewContext();

        // Two independent contexts read the same tent row / same token.
        var tentSeenByA = await contextA.Tents.SingleAsync(t => t.Id == tentId);
        var tentSeenByB = await contextB.Tents.SingleAsync(t => t.Id == tentId);
        Assert.Equal(tentSeenByA.RowVersion, tentSeenByB.RowVersion);

        contextA.Reservations.Add(NewReservation(tentId, userId, startDay: 10));
        contextB.Reservations.Add(NewReservation(tentId, userId, startDay: 20));

        // A commits first and rotates tent 1's RowVersion in the database.
        await contextA.SaveChangesAsync();

        // B still carries the old RowVersion -> its UPDATE matches no rows.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task A_successful_booking_rotates_the_tent_RowVersion()
    {
        _database.RequireAvailable();

        const int tentId = 2;
        const int userId = 1;

        byte[]? tokenBefore;
        await using (var arrangeContext = _database.NewContext())
        {
            tokenBefore = (await arrangeContext.Tents
                .AsNoTracking()
                .SingleAsync(t => t.Id == tentId)).RowVersion;
        }

        await using (var actContext = _database.NewContext())
        {
            actContext.Reservations.Add(NewReservation(tentId, userId, startDay: 5));
            await actContext.SaveChangesAsync();
        }

        await using var assertContext = _database.NewContext();
        var tokenAfter = (await assertContext.Tents
            .AsNoTracking()
            .SingleAsync(t => t.Id == tentId)).RowVersion;

        Assert.NotNull(tokenAfter);
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task HasOverlapAsync_applies_the_half_open_date_rule_in_the_database()
    {
        _database.RequireAvailable();

        const int tentId = 3;

        // Booked: Feb 10 -> Feb 13 (nights of the 10th, 11th and 12th).
        await using (var arrange = _database.NewContext())
        {
            arrange.Reservations.Add(new Reservation
            {
                TentId = tentId,
                UserId = 1,
                StartDate = new DateOnly(2099, 2, 10),
                EndDate = new DateOnly(2099, 2, 13),
                Status = ReservationStatus.Confirmed,
                TotalPrice = 360m,
                CreatedAt = DateTime.UtcNow
            });
            // A cancelled booking never blocks the tent.
            arrange.Reservations.Add(new Reservation
            {
                TentId = tentId,
                UserId = 1,
                StartDate = new DateOnly(2099, 2, 20),
                EndDate = new DateOnly(2099, 2, 25),
                Status = ReservationStatus.Cancelled,
                TotalPrice = 600m,
                CreatedAt = DateTime.UtcNow
            });
            await arrange.SaveChangesAsync();
        }

        await using var context = _database.NewContext();
        var repository = new ReservationRepository(context);

        Task<bool> Overlaps(int fromDay, int toDay) => repository.HasOverlapAsync(
            tentId, new DateOnly(2099, 2, fromDay), new DateOnly(2099, 2, toDay),
            excludeReservationId: null, CancellationToken.None);

        Assert.True(await Overlaps(12, 15));    // starts inside the booking
        Assert.True(await Overlaps(8, 11));     // ends inside the booking
        Assert.True(await Overlaps(9, 14));     // covers the whole booking
        Assert.False(await Overlaps(13, 15));   // check-in on the check-out day is fine
        Assert.False(await Overlaps(7, 10));    // check-out on the check-in day is fine
        Assert.False(await Overlaps(21, 23));   // only a cancelled booking there
    }

    private static Reservation NewReservation(int tentId, int userId, int startDay) => new()
    {
        TentId = tentId,
        UserId = userId,
        StartDate = new DateOnly(2099, 1, startDay),
        EndDate = new DateOnly(2099, 1, startDay + 3),
        Status = ReservationStatus.Confirmed,
        TotalPrice = 100m,
        CreatedAt = DateTime.UtcNow
    };
}
