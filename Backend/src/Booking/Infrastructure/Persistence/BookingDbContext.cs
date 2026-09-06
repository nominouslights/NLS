using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Booking.Application.Integration;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Customers;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>
/// The Booking module's DbContext (Postgres schema "booking"). Tenant stamping and the
/// audit pipeline (event journal + aggregate snapshots + outbox) come from
/// <see cref="ModuleDbContext"/>; this class only maps Booking's own entities and their
/// query filters. The integration event mapper turns BookingDay lifecycle events into the
/// module's public contracts (booking-day-confirmed / booking-day-reverted) via the outbox.
/// Query handlers read the aggregate tables directly (no rm_* projections — the seat math
/// is derived per read, and the write shapes already serve the read side). The database
/// half of tenant enforcement (RLS) is enabled in the migration, keyed on the session
/// variable set by <see cref="TenantSessionInterceptor"/>.
/// </summary>
public sealed class BookingDbContext(
    DbContextOptions<BookingDbContext> options,
    ITenantContext tenantContext,
    BookingIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, BookingServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Domain.Bookings.Booking> Bookings => Set<Domain.Bookings.Booking>();

    public DbSet<BookingPassenger> BookingPassengers => Set<BookingPassenger>();

    public DbSet<BookingDay> BookingDays => Set<BookingDay>();

    public DbSet<BookingPolicy> BookingPolicies => Set<BookingPolicy>();

    public DbSet<CorridorBookingSettings> CorridorSettings => Set<CorridorBookingSettings>();

    public DbSet<CorridorLookup> CorridorLookups => Set<CorridorLookup>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new BookingConfiguration());
        modelBuilder.ApplyConfiguration(new BookingPassengerConfiguration());
        modelBuilder.ApplyConfiguration(new BookingDayConfiguration());
        modelBuilder.ApplyConfiguration(new BookingPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new CorridorBookingSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new CorridorLookupConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<Customer>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<Domain.Bookings.Booking>().HasQueryFilter(b => b.TenantId == TenantId);
        modelBuilder.Entity<BookingPassenger>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<BookingDay>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<BookingPolicy>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<CorridorBookingSettings>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<CorridorLookup>().HasQueryFilter(c => c.TenantId == TenantId);
    }
}
