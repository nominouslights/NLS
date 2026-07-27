using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class EnterInspectionCommandHandlerTests
{
    private static EnterInspectionCommand Command(
        DateTimeOffset performedAt,
        DateTimeOffset? certifiedAt) =>
        new(
            TestVehicles.TenantId,
            InspectionSource.Dispatcher,
            InspectionType.PostTrip,
            TripNumber: "TR-4818",
            VehicleId: Guid.NewGuid(),
            Unit: "U-04",
            DriverName: "J. Spence",
            EnteredBy: null,
            PerformedAt: performedAt,
            OdometerKm: 118_346,
            Checklist: [],
            Defects: [],
            Weather: [],
            TemperatureC: null,
            RoadConditions: [],
            Visibility: null,
            RoadAdvisories: null,
            FuelLevel: null,
            Issues: [],
            Attestations: [true, true, true],
            DriverSignatureName: "J. Spence",
            CertifiedAt: certifiedAt,
            FuelAdded: false,
            FuelLitres: null,
            FuelCostCad: null);

    [Fact]
    public async Task Non_utc_timestamps_are_normalized_to_utc_before_persistence()
    {
        // Client sends timestamps with a -05:00 (Eastern) offset. Npgsql rejects a non-UTC
        // DateTimeOffset for `timestamp with time zone`, which surfaced as a 500 on
        // POST /api/fleet/inspections. The handler must normalize to UTC (offset zero) while
        // preserving the instant.
        var performedAt = new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.FromHours(-5));
        var certifiedAt = new DateTimeOffset(2026, 7, 23, 9, 30, 0, TimeSpan.FromHours(-5));

        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new EnterInspectionCommandHandler(repository);

        var result = await handler.Handle(Command(performedAt, certifiedAt), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(repository.Inspections);

        Assert.Equal(TimeSpan.Zero, stored.PerformedAt.Offset);
        Assert.Equal(performedAt.UtcDateTime, stored.PerformedAt.UtcDateTime);

        Assert.NotNull(stored.CertifiedAt);
        Assert.Equal(TimeSpan.Zero, stored.CertifiedAt!.Value.Offset);
        Assert.Equal(certifiedAt.UtcDateTime, stored.CertifiedAt!.Value.UtcDateTime);
    }

    [Fact]
    public async Task A_null_certified_at_stays_null()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new EnterInspectionCommandHandler(repository);

        var result = await handler.Handle(
            Command(DateTimeOffset.UtcNow, certifiedAt: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(repository.Inspections);
        Assert.Null(stored.CertifiedAt);
    }
}
