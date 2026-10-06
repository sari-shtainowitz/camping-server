using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;
using CampingDbContext = Camping.Data.DbContext;

namespace Camping.Tests;

/// <summary>
/// Owns a dedicated PostgreSQL database for the Step 11 concurrency tests.
///
/// The connection string comes from <c>appsettings.json</c>
/// (<c>ConnectionStrings:CampingTestDb</c>) or the <c>CAMPING_TEST_DB</c>
/// environment variable. It uses the same local PostgreSQL instance the
/// application uses, but a SEPARATE database (<c>CampingDb_ConcurrencyTests</c>):
/// <see cref="InitializeAsync"/> drops and re-creates it from the migrations on
/// every run, and <see cref="DisposeAsync"/> drops it again - so the tests never
/// read or corrupt the application's own <c>CampingDb</c>.
///
/// If no PostgreSQL instance is reachable, the fixture does not throw: it records
/// <see cref="Available"/> = false and <see cref="UnavailableReason"/>, and each
/// test fails fast with that single explanatory message rather than a stack of
/// connection errors. No container runtime is used or required.
///
/// (Name kept as <c>SqlServerTestDatabase</c>/<see cref="SqlServerTestCollection"/>
/// across the SQL Server -> PostgreSQL migration to minimize diff; only the
/// provider internals below changed.)
/// </summary>
public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    public string ConnectionString { get; }

    public bool Available { get; private set; }

    public string? UnavailableReason { get; private set; }

    public SqlServerTestDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        ConnectionString =
            Environment.GetEnvironmentVariable("CAMPING_TEST_DB")
            ?? configuration.GetConnectionString("CampingTestDb")
            ?? throw new InvalidOperationException(
                "No test connection string. Set ConnectionStrings:CampingTestDb in appsettings.json or the CAMPING_TEST_DB environment variable.");
    }

    /// <summary>Creates a brand-new context bound to the dedicated test database.</summary>
    public CampingDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CampingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    /// <summary>Throws with <see cref="UnavailableReason"/> when PostgreSQL was not reachable.</summary>
    public void RequireAvailable()
    {
        if (!Available)
        {
            throw new InvalidOperationException(UnavailableReason);
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await using var db = NewContext();
            await db.Database.EnsureDeletedAsync();   // remove any leftover from a previous run
            await db.Database.MigrateAsync();         // schema + HasData seed (tents 1..5, users 1..2)
            Available = true;
        }
        catch (Exception ex) when (IsServerUnreachable(ex))
        {
            Available = false;
            UnavailableReason =
                $"PostgreSQL is not reachable at {SafeTarget()}. Step 11 runtime verification is " +
                $"BLOCKED until a local PostgreSQL instance is available (or CAMPING_TEST_DB points at one). " +
                $"Underlying error: {ex.GetBaseException().Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (!Available)
        {
            return;
        }

        try
        {
            await using var db = NewContext();
            await db.Database.EnsureDeletedAsync();   // leave nothing behind
        }
        catch
        {
            // Best-effort cleanup; a failure here must not mask test results.
        }
    }

    // A PostgreSQL server that is down / unresolvable surfaces as an NpgsqlException
    // whose base exception is a Win32Exception or socket-level failure (connection
    // refused, host unreachable, DNS failure). A failure that is NOT connectivity
    // (bad migration, permission denied, wrong credentials) is left to throw so it
    // is not silently reported as "server unavailable".
    private static bool IsServerUnreachable(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is System.Net.Sockets.SocketException
                || e is System.ComponentModel.Win32Exception)
            {
                return true;
            }
        }

        return false;
    }

    private string SafeTarget()
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
            return $"{builder.Host}:{builder.Port}/{builder.Database}";
        }
        catch
        {
            return "the configured connection string";
        }
    }
}

/// <summary>
/// Binds every concurrency test class to the single <see cref="SqlServerTestDatabase"/>
/// instance, so the database is prepared once per test run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerTestCollection : ICollectionFixture<SqlServerTestDatabase>
{
    public const string Name = "PostgreSQL concurrency database";
}
