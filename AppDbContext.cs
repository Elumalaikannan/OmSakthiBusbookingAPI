using Microsoft.EntityFrameworkCore;

namespace BusBooking.Api;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    JsonDatabaseStore jsonDatabaseStore) : DbContext(options)
{
    private readonly JsonDatabaseStore _jsonDatabaseStore = jsonDatabaseStore;

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Bus> Buses => Set<Bus>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateData();
        var changes = base.SaveChanges(acceptAllChangesOnSuccess);
        _jsonDatabaseStore.SaveAsync(this).GetAwaiter().GetResult();
        return changes;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateData();
        var changes = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await _jsonDatabaseStore.SaveAsync(this, cancellationToken);
        return changes;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Bus>().HasIndex(x => x.RegistrationNumber).IsUnique();
        modelBuilder.Entity<Bus>().Property(x => x.RegistrationNumber).HasMaxLength(32);
        modelBuilder.Entity<Trip>()
            .HasOne(x => x.Bus)
            .WithMany(x => x.Trips)
            .HasForeignKey(x => x.BusId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Booking>().HasIndex(x => new { x.TripId, x.SeatNumber })
            .IsUnique();
        modelBuilder.Entity<Booking>()
            .HasOne(x => x.User)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Booking>()
            .HasOne(x => x.Trip)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PasswordResetToken>().HasIndex(x => x.TokenHash).IsUnique();
    }

    private void ValidateData()
    {
        ChangeTracker.DetectChanges();
        EnsureUnique(GetCurrentRows(Users, user => user.Id).Select(user => user.Email),
            StringComparer.OrdinalIgnoreCase,
            "An account with that email already exists.");
        EnsureUnique(GetCurrentRows(Buses, bus => bus.Id).Select(bus => bus.RegistrationNumber),
            StringComparer.OrdinalIgnoreCase,
            "A bus with that registration number already exists.");
        EnsureUnique(GetCurrentRows(Bookings, booking => booking.Id)
            .Where(booking => booking.Status == "Confirmed")
            .Select(booking => (booking.TripId, booking.SeatNumber)),
            EqualityComparer<(int TripId, int SeatNumber)>.Default,
            "The seat was just booked.");
        EnsureUnique(GetCurrentRows(PasswordResetTokens, token => token.Id)
            .Select(token => token.TokenHash),
            StringComparer.Ordinal,
            "A password reset token with that hash already exists.");
    }

    private List<TEntity> GetCurrentRows<TEntity>(DbSet<TEntity> set, Func<TEntity, int> getId)
        where TEntity : class
    {
        var rows = set.AsNoTracking().ToDictionary(getId);
        var newRows = new List<TEntity>();
        foreach (var entry in ChangeTracker.Entries<TEntity>())
        {
            var id = getId(entry.Entity);
            if (entry.State == EntityState.Deleted)
                rows.Remove(id);
            else if (entry.State == EntityState.Added && id == 0)
                newRows.Add(entry.Entity);
            else
                rows[id] = entry.Entity;
        }

        return rows.Values.Concat(newRows).ToList();
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, IEqualityComparer<T> comparer, string message)
    {
        if (values.GroupBy(value => value, comparer).Any(group => group.Count() > 1))
            throw new DbUpdateException(message);
    }
}
