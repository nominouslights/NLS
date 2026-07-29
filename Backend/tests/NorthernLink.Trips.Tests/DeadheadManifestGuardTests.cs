using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Trips.Application.Manifests.Create;
using NorthernLink.Trips.Application.Trips.AttachManifest;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Deadheads carry no passengers, so a manifest can neither be created for one
/// (create-time guard) nor linked to one by the async attach reaction (defense in depth).
/// </summary>
public class DeadheadManifestGuardTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripManifestRepository _manifests = new();

    private static CreateTripManifestCommand CreateCommand(string tripNumber) => new(
        TestManifests.TenantId,
        TripDate: new DateOnly(2026, 7, 21),
        TripNumber: tripNumber,
        Route: "Thompson → Lynn Lake",
        Direction: TripDirection.Outbound,
        Client: null,
        Passengers: TestManifests.OnePassenger(),
        AllSeatbeltsVerified: true,
        Cargo: [],
        AllCargoSecured: CargoSecuredStatus.NotApplicable,
        Source: ManifestSource.Dispatcher,
        EnteredBy: "R. Dispatch");

    [Fact]
    public async Task Creating_a_manifest_for_a_deadhead_trip_is_refused()
    {
        var deadhead = TestPlanning.ScheduleTrip(tripNumber: "TR-7001", isEmptyLeg: true).Value;
        _trips.Add(deadhead);
        var handler = new CreateTripManifestCommandHandler(_manifests, _trips);

        var result = await handler.Handle(CreateCommand("TR-7001"), CancellationToken.None);

        Assert.Equal(TripErrors.ManifestNotAllowedForEmptyLeg, result.Error);
        Assert.Empty(_manifests.Manifests);
        Assert.Equal(0, _manifests.SaveCount);
    }

    [Fact]
    public async Task Creating_a_manifest_for_a_normal_or_unknown_trip_number_still_works()
    {
        var regular = TestPlanning.ScheduleTrip(tripNumber: "TR-7002").Value;
        _trips.Add(regular);
        var handler = new CreateTripManifestCommandHandler(_manifests, _trips);

        var forRegular = await handler.Handle(CreateCommand("TR-7002"), CancellationToken.None);
        // Manifests associate by trip number lazily — one with no matching trip yet is fine.
        var forUnknown = await handler.Handle(CreateCommand("TR-7999"), CancellationToken.None);

        Assert.True(forRegular.IsSuccess);
        Assert.True(forUnknown.IsSuccess);
        Assert.Equal(2, _manifests.Manifests.Count);
    }

    [Fact]
    public async Task Attach_reaction_skips_a_deadhead_trip_without_linking()
    {
        var deadhead = TestPlanning.ScheduleTrip(tripNumber: "TR-7003", isEmptyLeg: true).Value;
        _trips.Add(deadhead);
        var manifest = TestManifests.Create(tripNumber: "TR-7003").Value;
        _manifests.Add(manifest);
        var handler = new AttachManifestToTripCommandHandler(
            _manifests, _trips, NullLogger<AttachManifestToTripCommandHandler>.Instance);

        var result = await handler.Handle(new AttachManifestToTripCommand(manifest.Id), CancellationToken.None);

        Assert.True(result.IsSuccess); // logged-and-skipped, never an error loop
        Assert.Null(deadhead.ManifestId);
        Assert.Equal(0, _trips.SaveCount);
    }
}
