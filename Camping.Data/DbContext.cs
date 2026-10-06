using Camping.Core.Entities;
using Camping.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Camping.Data;

/// <summary>
/// EF Core context for the camping reservation domain.
/// Named "DbContext" per the Step 3 specification; it derives from
/// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
/// </summary>
public class DbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public DbContext(DbContextOptions<DbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<CampingSite> CampingSites => Set<CampingSite>();
    public DbSet<Tent> Tents => Set<Tent>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Amenity> Amenities => Set<Amenity>();

    public override int SaveChanges()
    {
        RotateTentConcurrencyTokens();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RotateTentConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RotateTentConcurrencyTokens();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RotateTentConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Central maintenance of the tent optimistic-concurrency token (Step 11).
    /// A tent is the limited resource: whenever it is written directly, or a
    /// reservation that points at it is added or changed, its
    /// <see cref="Tent.RowVersion"/> is given a fresh value. Two transactions
    /// that touch the same tent therefore cannot both commit - the second one's
    /// UPDATE matches zero rows and EF raises
    /// <see cref="DbUpdateConcurrencyException"/>. Keeping this here means no
    /// service or repository has to remember to bump the token.
    /// </summary>
    private void RotateTentConcurrencyTokens()
    {
        ChangeTracker.DetectChanges();

        // Pull the tent that each added/changed reservation consumes into the
        // save, even when none of the tent's own columns changed - loading it
        // first if the caller never tracked it. Forcing the tent's row into the
        // UPDATE is what makes it the real point of contention: two reservations
        // for the same tent both rewrite its RowVersion, so whichever commits
        // second matches zero rows and loses.
        foreach (var reservation in ChangeTracker.Entries<Reservation>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var tentId = reservation.Entity.TentId;

            var tentEntry = ChangeTracker.Entries<Tent>()
                .FirstOrDefault(t => t.Entity.Id == tentId);

            if (tentEntry is null)
            {
                var tent = Tents.Find(tentId);   // reads the row and tracks it as Unchanged
                if (tent is not null)
                {
                    tentEntry = Entry(tent);
                }
            }

            if (tentEntry is { State: EntityState.Unchanged })
            {
                tentEntry.Property(nameof(Tent.RowVersion)).IsModified = true;
            }
        }

        // Every tent taking part in this save gets a brand-new token value.
        foreach (var tentEntry in ChangeTracker.Entries<Tent>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            tentEntry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUser(modelBuilder.Entity<User>());
        ConfigureCampingSite(modelBuilder.Entity<CampingSite>());
        ConfigureTent(modelBuilder.Entity<Tent>());
        ConfigureReservation(modelBuilder.Entity<Reservation>());
        ConfigureAmenity(modelBuilder.Entity<Amenity>());

        Seed(modelBuilder);
    }

    // Fixed timestamp so migrations stay deterministic (HasData forbids DateTime.Now).
    private static readonly DateTime SeedTimestamp =
        new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Amenity>().HasData(
            new Amenity { Id = 1, Name = "WiFi", Description = "Wireless internet across the site." },
            new Amenity { Id = 2, Name = "Hot Showers", Description = "Shared shower block with hot water." },
            new Amenity { Id = 3, Name = "Electricity Hook-up", Description = "230V power point at the pitch." },
            new Amenity { Id = 4, Name = "Parking", Description = "On-site parking next to the pitches." });

        // The domain has a single camping site. All tents belong to it.
        modelBuilder.Entity<CampingSite>().HasData(
            new CampingSite
            {
                Id = 1,
                Name = "Lakeside Camping",
                Description = "Shaded pitches on the shore of the lake.",
                Address = "1 Lake Road, Tiberias",
                CreatedAt = SeedTimestamp
            });

        // CampingSite >--< Amenity join rows (implicit join entity "AmenityCampingSite").
        modelBuilder.Entity("AmenityCampingSite").HasData(
            new { CampingSitesId = 1, AmenitiesId = 1 },
            new { CampingSitesId = 1, AmenitiesId = 2 },
            new { CampingSitesId = 1, AmenitiesId = 3 });

        modelBuilder.Entity<Tent>().HasData(
            new Tent { Id = 1, CampingSiteId = 1, Label = "A-1", Type = TentType.Standard, Capacity = 2, PricePerNight = 40.00m, IsActive = true },
            new Tent { Id = 2, CampingSiteId = 1, Label = "A-2", Type = TentType.Family, Capacity = 4, PricePerNight = 65.00m, IsActive = true },
            new Tent { Id = 3, CampingSiteId = 1, Label = "A-3", Type = TentType.Glamping, Capacity = 2, PricePerNight = 120.00m, IsActive = true },
            new Tent { Id = 4, CampingSiteId = 1, Label = "B-1", Type = TentType.Standard, Capacity = 3, PricePerNight = 45.00m, IsActive = true },
            new Tent { Id = 5, CampingSiteId = 1, Label = "B-2", Type = TentType.Family, Capacity = 5, PricePerNight = 70.00m, IsActive = false });

        // Demo accounts, one per role (Step 16). The hashes are produced by
        // Pbkdf2PasswordHasher; the plaintext passwords are documented in the
        // root README. Change them before any real deployment.
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FullName = "Casey Customer",
                Email = "customer@camping.test",
                PhoneNumber = "+972-50-000-0001",
                Role = UserRole.Customer,
                PasswordHash = "pbkdf2.sha256.210000.WE/sJTrKkksZ6SL1f3Trfg==.AulcXRbR3C6OqYDaV1nEiOFqd/W6u2W0mRBMo2Bwm8g=", // Customer123!
                CreatedAt = SeedTimestamp
            },
            new User
            {
                Id = 2,
                FullName = "Alex Admin",
                Email = "admin@camping.test",
                PhoneNumber = "+972-50-000-0002",
                Role = UserRole.Admin,
                PasswordHash = "pbkdf2.sha256.210000.Xb/NAxE7YLoA9GwLCXVWlg==.jH6tuKffPG18AcaaZmNUKCcKm+x2zsihUmNJ34jlOpo=", // Admin123!
                CreatedAt = SeedTimestamp
            });

        modelBuilder.Entity<Reservation>().HasData(
            new Reservation
            {
                Id = 1,
                TentId = 1,
                UserId = 1,
                StartDate = new DateOnly(2025, 7, 1),
                EndDate = new DateOnly(2025, 7, 4),
                Status = ReservationStatus.Confirmed,
                TotalPrice = 120.00m,
                CreatedAt = SeedTimestamp
            });
    }

    private static void ConfigureUser(EntityTypeBuilder<User> user)
    {
        user.HasKey(u => u.Id);

        user.Property(u => u.FullName).IsRequired().HasMaxLength(100);
        user.Property(u => u.Email).IsRequired().HasMaxLength(256);
        user.Property(u => u.PhoneNumber).HasMaxLength(30);
        user.Property(u => u.PasswordHash).HasMaxLength(500);
        user.Property(u => u.Role).HasConversion<int>();

        user.HasIndex(u => u.Email).IsUnique();

        // User 1 --< Reservation
        user.HasMany(u => u.Reservations)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCampingSite(EntityTypeBuilder<CampingSite> site)
    {
        site.HasKey(s => s.Id);

        site.Property(s => s.Name).IsRequired().HasMaxLength(100);
        site.Property(s => s.Description).HasMaxLength(1000);
        site.Property(s => s.Address).IsRequired().HasMaxLength(200);

        site.HasIndex(s => s.Name).IsUnique();

        // CampingSite 1 --< Tent
        site.HasMany(s => s.Tents)
            .WithOne(t => t.CampingSite)
            .HasForeignKey(t => t.CampingSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        // CampingSite >--< Amenity (many-to-many)
        site.HasMany(s => s.Amenities)
            .WithMany(a => a.CampingSites)
            .UsingEntity(join => join.ToTable("CampingSiteAmenity"));
    }

    private static void ConfigureTent(EntityTypeBuilder<Tent> tent)
    {
        tent.HasKey(t => t.Id);

        tent.Property(t => t.Label).IsRequired().HasMaxLength(50);
        tent.Property(t => t.Type).HasConversion<int>();
        tent.Property(t => t.PricePerNight).HasColumnType("decimal(10,2)");

        // Optimistic concurrency token on the limited resource (Step 11).
        // RowVersion is an application-managed concurrency token, NOT a
        // store-generated SQL Server 'rowversion': its value is rotated in
        // SaveChanges (see RotateTentConcurrencyTokens). It maps to a
        // fixed-width binary column - the 16 bytes of a Guid. PostgreSQL has no
        // 'varbinary' type (and 'bytea' takes no length qualifier), so this is
        // "bytea" under Npgsql instead of SQL Server's "varbinary(16)" - same
        // token, same concurrency mechanism, different provider's raw type name.
        tent.Property(t => t.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .HasColumnType("bytea");

        tent.HasIndex(t => new { t.CampingSiteId, t.Label }).IsUnique();

        // Tent 1 --< Reservation
        tent.HasMany(t => t.Reservations)
            .WithOne(r => r.Tent)
            .HasForeignKey(r => r.TentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // EF Core 6 + SQL Server has no native DateOnly mapping (that arrived in
    // EF Core 8), so StartDate/EndDate are stored through a DateOnly <-> DateTime
    // converter against a 'date' column. Equality, range (<,>,<=,>=) and ORDER BY
    // over these properties still translate to SQL.
    private static readonly ValueConverter<DateOnly, DateTime> DateOnlyConverter = new(
        dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
        dateTime => DateOnly.FromDateTime(dateTime));

    private static void ConfigureReservation(EntityTypeBuilder<Reservation> reservation)
    {
        reservation.HasKey(r => r.Id);

        reservation.Property(r => r.StartDate).HasConversion(DateOnlyConverter).HasColumnType("date");
        reservation.Property(r => r.EndDate).HasConversion(DateOnlyConverter).HasColumnType("date");
        reservation.Property(r => r.Status).HasConversion<int>();
        reservation.Property(r => r.TotalPrice).HasColumnType("decimal(10,2)");

        reservation.HasIndex(r => new { r.TentId, r.StartDate, r.EndDate });

        // Every non-Admin reservation list is now filtered by UserId (the
        // controller forces this for ownership enforcement) and, by default,
        // sorted by StartDate (ReservationRepository.ApplySort's default). The
        // existing single-column UserId index can serve the filter alone; this
        // composite also covers the combined filter+sort together.
        reservation.HasIndex(r => new { r.UserId, r.StartDate });
    }

    private static void ConfigureAmenity(EntityTypeBuilder<Amenity> amenity)
    {
        amenity.HasKey(a => a.Id);

        amenity.Property(a => a.Name).IsRequired().HasMaxLength(80);
        amenity.Property(a => a.Description).HasMaxLength(500);

        amenity.HasIndex(a => a.Name).IsUnique();
    }
}
