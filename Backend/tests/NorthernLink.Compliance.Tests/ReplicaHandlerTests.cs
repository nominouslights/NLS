using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// The three replicas. A fan-out subscription replays a routing key's whole history, so "the same
/// event twice" and "an old event after a newer one" are normal: upserts converge on one row per
/// key, overwrite every event-carried column, and never touch the Compliance-only override.
/// </summary>
public class ReplicaHandlerTests
{
    private readonly InMemoryLookupStore _lookups = new();

    private static VehicleChangedIntegrationEvent Vehicle(int seating = 15, string status = "Active", string unit = "101") =>
        new(TestCompliance.VehicleId, TestCompliance.TenantId, unit, status, "2", seating, "Owned");

    [Fact]
    public async Task A_vehicle_event_replayed_upserts_one_row_and_keeps_the_override()
    {
        var handler = new VehicleChangedIntegrationEventHandler(_lookups, NullLogger<VehicleChangedIntegrationEventHandler>.Instance);

        await handler.Handle(Vehicle(), CancellationToken.None);
        await _lookups.SetRegulatedOverrideAsync(TestCompliance.VehicleId, false);
        await handler.Handle(Vehicle(seating: 24, status: "InMaintenance", unit: "102"), CancellationToken.None);
        await handler.Handle(Vehicle(seating: 24, status: "InMaintenance", unit: "102"), CancellationToken.None);

        var row = Assert.Single(_lookups.Vehicles);
        Assert.Equal("102", row.UnitNumber);
        Assert.Equal(24, row.SeatingCapacity);
        Assert.Equal("InMaintenance", row.Status);
        Assert.False(row.RegulatedOverride);
        Assert.False(row.IsRegulated(11));
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(11, true)]
    [InlineData(24, true)]
    public void Without_an_override_seating_decides(int seating, bool regulated)
    {
        var row = new VehicleLookup { SeatingCapacity = seating, Status = "Active" };

        Assert.Equal(regulated, row.IsRegulated(11));
    }

    [Fact]
    public async Task A_retired_vehicle_stays_in_the_replica_but_leaves_readiness()
    {
        var handler = new VehicleChangedIntegrationEventHandler(_lookups, NullLogger<VehicleChangedIntegrationEventHandler>.Instance);

        await handler.Handle(Vehicle(status: "Retired"), CancellationToken.None);

        Assert.False(Assert.Single(_lookups.Vehicles).CountsForReadiness);
    }

    [Fact]
    public async Task Replica_writes_run_under_the_events_tenant()
    {
        var spy = new TenantSpy(_lookups);
        var handler = new VehicleChangedIntegrationEventHandler(spy, NullLogger<VehicleChangedIntegrationEventHandler>.Instance);

        await handler.Handle(Vehicle(), CancellationToken.None);

        Assert.Equal(TestCompliance.TenantId, spy.AmbientTenantAtUpsert);
    }

    [Fact]
    public async Task A_driver_event_upserts_and_tells_observers_what_changed()
    {
        var observer = new RecordingObserver();
        var handler = new DriverChangedIntegrationEventHandler(_lookups, [observer], NullLogger<DriverChangedIntegrationEventHandler>.Instance);
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await handler.Handle(new DriverChangedIntegrationEvent(driverId, TestCompliance.TenantId, "J. Smith", "4", "Active", "Hired"), CancellationToken.None);
        await handler.Handle(new DriverChangedIntegrationEvent(driverId, TestCompliance.TenantId, "J. Smith", "4", "Deactivated", "Hired", userId), CancellationToken.None);

        var row = Assert.Single(_lookups.Drivers);
        Assert.Equal("Deactivated", row.Status);
        Assert.Equal(userId, row.UserId);
        Assert.Equal(2, observer.Calls.Count);
        Assert.Null(observer.Calls[0].Before);
        Assert.Equal("Active", observer.Calls[1].Before!.Status);
        Assert.Equal("Deactivated", observer.Calls[1].After.Status);
    }

    [Fact]
    public async Task A_user_event_upserts_one_row_and_clears_a_removed_name()
    {
        var handler = new UserChangedIntegrationEventHandler(_lookups, NullLogger<UserChangedIntegrationEventHandler>.Instance);
        var userId = Guid.NewGuid();

        await handler.Handle(new UserChangedIntegrationEvent(userId, TestCompliance.TenantId, "co@northernlink.ca", "ComplianceOfficer", "Jo Officer", null), CancellationToken.None);
        await handler.Handle(new UserChangedIntegrationEvent(userId, TestCompliance.TenantId, "co@northernlink.ca", "ComplianceOfficer", null, null), CancellationToken.None);

        var row = Assert.Single(_lookups.Users);
        Assert.Null(row.FullName);
        Assert.Equal("ComplianceOfficer", row.Role);
    }

    private sealed class RecordingObserver : IDriverReplicaObserver
    {
        public List<(DriverLookup? Before, DriverLookup After)> Calls { get; } = [];

        public Task OnDriverChangedAsync(DriverLookup? before, DriverLookup after, CancellationToken cancellationToken)
        {
            Calls.Add((before, after));
            return Task.CompletedTask;
        }
    }

    private sealed class TenantSpy(InMemoryLookupStore inner) : Application.Abstractions.IVehicleLookupRepository
    {
        public Guid? AmbientTenantAtUpsert { get; private set; }

        public Task<VehicleLookup?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default) => inner.GetAsync(vehicleId, cancellationToken);

        public Task<IReadOnlyList<VehicleLookup>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);

        public Task UpsertAsync(VehicleLookup vehicle, CancellationToken cancellationToken = default)
        {
            AmbientTenantAtUpsert = AmbientTenant.Current;
            return inner.UpsertAsync(vehicle, cancellationToken);
        }

        public Task<bool> SetRegulatedOverrideAsync(Guid vehicleId, bool? regulatedOverride, CancellationToken cancellationToken = default) =>
            inner.SetRegulatedOverrideAsync(vehicleId, regulatedOverride, cancellationToken);
    }
}
