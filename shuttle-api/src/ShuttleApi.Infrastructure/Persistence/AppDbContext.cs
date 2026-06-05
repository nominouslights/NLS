using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.CommunityCalendar;
using ShuttleApi.Domain.Drivers;
using ShuttleApi.Domain.Locations;
using ShuttleApi.Domain.Trips;
using ShuttleApi.Domain.Users;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientNotificationEmail> ClientNotificationEmails => Set<ClientNotificationEmail>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractRateLine> ContractRateLines => Set<ContractRateLine>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<DriverRosterEntry> DriverRosterEntries => Set<DriverRosterEntry>();
    public DbSet<DocumentFileBlob> DocumentFileBlobs => Set<DocumentFileBlob>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripStop> TripStops => Set<TripStop>();
    public DbSet<TripPassenger> TripPassengers => Set<TripPassenger>();
    public DbSet<TripPreInspection> TripPreInspections => Set<TripPreInspection>();
    public DbSet<TripInspectionItem> TripInspectionItems => Set<TripInspectionItem>();
    public DbSet<TripPostReport> TripPostReports => Set<TripPostReport>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleServiceRecord> VehicleServiceRecords => Set<VehicleServiceRecord>();
    public DbSet<VehicleInspectionRecord> VehicleInspectionRecords => Set<VehicleInspectionRecord>();
    public DbSet<SavedLocation> SavedLocations => Set<SavedLocation>();
    public DbSet<CommunityCalendarBlock> CommunityCalendarBlocks => Set<CommunityCalendarBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events from all modified aggregates before saving
        var aggregates = ChangeTracker.Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var auditEvents = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents.Select(evt => new AuditEvent
            {
                Id = evt.EventId,
                EventType = evt.EventType,
                AggregateType = aggregate.GetType().Name,
                AggregateId = aggregate.GetId()?.ToString() ?? string.Empty,
                Payload = JsonSerializer.Serialize(evt, evt.GetType()),
                OccurredOn = evt.OccurredOn
            }))
            .ToList();

        if (auditEvents.Count > 0)
            AuditEvents.AddRange(auditEvents);

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        return result;
    }
}
