using Camping.Core.Entities;
using Camping.Core.Enums;
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
