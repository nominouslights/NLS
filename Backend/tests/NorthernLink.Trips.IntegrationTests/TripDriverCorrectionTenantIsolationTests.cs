using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Trips.CorrectDriver;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using NorthernLink.Trips.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Trips.IntegrationTests;

/// <summary>
/// A driver correction names a driver by id, and the handler resolves it through the
/// driver_lookup replica. These prove, against real Postgres as the non-superuser app role (so
/// RLS applies), that a driver row belonging to ANOTHER tenant is invisible to that lookup — the
/// correction is refused with DriverNotFound and nothing is written: not the trip, not the
/// journal. A same-tenant control proves the test is not passing for some unrelated reason.
/// </summary>
[Collection("postgres")]
public class TripDriverCorrectionTenantIsolationTests(PostgresFixture fixture)
{
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly string CorrectedEventType = AuditNames.ForEvent(typeof(TripDriverCorrectedDomainEvent));

    [Fact]
    public async Task A_driver_from_another_tenant_is_DriverNotFound_and_nothing_is_saved()
    {
        var originalDriverId = Guid.NewGuid();
        var tripId = await SeedFinishedTripAsync(originalDriverId);
        var foreignDriverId = await SeedDriverAsync(TenantB, "Other-tenant driver");

        // Belt and braces: even with the EF tenant filter switched off, RLS alone hides the
        // other tenant's row from a tenant-A session.
        await using (var probe = fixture.CreateTripsContext(PostgresFixture.TenantA))
        {
            Assert.False(await probe.DriverLookups.IgnoreQueryFilters().AnyAsync(d => d.DriverId == foreignDriverId));
        }

        var result = await CorrectAsync(tripId, foreignDriverId);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.DriverNotFound, result.Error);

        await using var verify = fixture.CreateTripsContext(PostgresFixture.TenantA);
        var trip = await verify.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(originalDriverId, trip.DriverId);
        Assert.Equal("R. Ballantyne", trip.DriverName);
        Assert.False(await verify.Set<EventJournalEntry>()
            .AnyAsync(e => e.AggregateId == tripId && e.EventType == CorrectedEventType));
    }

    [Fact]
    public async Task Control_a_driver_from_the_same_tenant_is_corrected_and_journaled()
    {
        var tripId = await SeedFinishedTripAsync(Guid.NewGuid());
        var ownDriverId = await SeedDriverAsync(PostgresFixture.TenantA, "J. Moose");

        var result = await CorrectAsync(tripId, ownDriverId);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
        await using var verify = fixture.CreateTripsContext(PostgresFixture.TenantA);
        var trip = await verify.Trips.AsNoTracking().SingleAsync(t => t.Id == tripId);
        Assert.Equal(ownDriverId, trip.DriverId);
        Assert.Equal("J. Moose", trip.DriverName);
        Assert.True(await verify.Set<EventJournalEntry>()
            .AnyAsync(e => e.AggregateId == tripId && e.EventType == CorrectedEventType));
    }

    private async Task<Result> CorrectAsync(Guid tripId, Guid driverId)
    {
        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);
        var handler = new CorrectTripDriverCommandHandler(
            new TripRepository(context), new DriverLookupRepository(context), new DispatcherActor());
        return await handler.Handle(
            new CorrectTripDriverCommand(tripId, driverId, "Logged against the wrong driver"), CancellationToken.None);
    }

    /// <summary>A clientless trip run through to Completed — a status that accepts a correction.</summary>
    private async Task<Guid> SeedFinishedTripAsync(Guid driverId)
    {
        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);

        var trip = Trip.Schedule(
            PostgresFixture.TenantA,
            $"TR-DC-{Guid.NewGuid():N}"[..20],
            serviceDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
            windowStart: new TimeOnly(8, 0),
            windowEnd: null,
            serviceType: TripServiceType.Charter,
            routeId: null,
            routeName: "Thompson – Lynn Lake",
            origin: "Thompson",
            destination: "Lynn Lake",
            stops: Array.Empty<RouteStop>(),
            distanceKm: 320,
            scheduleTemplateId: null,
            roundTripKey: null,
            direction: null,
            isEmptyLeg: false,
            clientId: null,
            clientName: null,
            poNumber: null,
            driverId: driverId,
            driverName: "R. Ballantyne",
            vehicleId: Guid.NewGuid(),
            vehicleUnit: "U-04",
            seatsCapacity: null,
            seatsMinimum: null);
        Assert.True(trip.IsSuccess, trip.IsFailure ? trip.Error.Code : "");
        trip.Value.RecordPostTripInspection();
        Assert.True(trip.Value.FinishOperations().IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Value.Status);

        context.Trips.Add(trip.Value);
        await context.SaveChangesAsync();
        return trip.Value.Id;
    }

    /// <summary>Seeds a driver_lookup row under its own tenant's session, as the replica handler would.</summary>
    private async Task<Guid> SeedDriverAsync(Guid tenantId, string name)
    {
        var driverId = Guid.NewGuid();
        await using var context = fixture.CreateTripsContext(tenantId);
        context.DriverLookups.Add(new DriverLookup
        {
            DriverId = driverId,
            TenantId = tenantId,
            Name = name,
            LicenceClass = "Class 4",
            Status = DriverLookup.ActiveStatus,
            UserId = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
        return driverId;
    }

    private sealed class DispatcherActor : ICurrentActor
    {
        public Guid? UserId { get; } = Guid.Parse("00000000-0000-0000-0000-00000000aa09");

        public string? Email => "dispatch@northernlink.ca";

        public IReadOnlyCollection<string> Roles { get; } = [Shared.Kernel.Roles.Dispatcher];
    }
}
