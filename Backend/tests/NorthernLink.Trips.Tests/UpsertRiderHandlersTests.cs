using NorthernLink.Trips.Application.Riders;
using NorthernLink.Trips.Application.Riders.UpsertFromManifest;
using NorthernLink.Trips.Application.Riders.UpsertFromTrip;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The rider-directory upsert reactions: <c>TripManifestLinkedDomainEvent</c> →
/// <see cref="UpsertRidersFromTripCommand"/> (create path) and
/// <c>TripManifestUpdatedDomainEvent</c> → <see cref="UpsertRidersFromManifestCommand"/>
/// (edit path), both through <see cref="ManifestRiderUpserter"/>.
/// </summary>
public class UpsertRiderHandlersTests
{
    private readonly FakeTripManifestRepository _manifests = new();
    private readonly FakeTripRepository _trips = new();
    private readonly FakeRiderRepository _riders = new();

    private UpsertRidersFromTripCommandHandler FromTripHandler =>
        new(_trips, _manifests, new ManifestRiderUpserter(_riders));

    private UpsertRidersFromManifestCommandHandler FromManifestHandler =>
        new(_manifests, _trips, new ManifestRiderUpserter(_riders));

    private static ManifestPassenger Passenger(string name, string? email = null, string? phone = null) =>
        new() { Name = name, Email = email, Phone = phone };

    /// <summary>A manifest linked to a trip of the given service type, both stored in the fakes.</summary>
    private (TripManifest Manifest, Trip Trip) LinkedManifest(
        TripServiceType serviceType,
        IReadOnlyList<ManifestPassenger> passengers,
        string tripNumber = "TR-4821")
    {
        var manifest = TestManifests.Create(tripNumber: tripNumber, passengers: passengers.ToList()).Value;
        _manifests.Add(manifest);
        var trip = TestPlanning.ScheduleTrip(tripNumber: tripNumber, serviceType: serviceType).Value;
        Assert.True(trip.AttachManifest(manifest.Id).IsSuccess);
        _trips.Add(trip);
        return (manifest, trip);
    }

    [Fact]
    public async Task A_linked_contract_crew_trip_creates_a_rider_per_passenger()
    {
        var (_, trip) = LinkedManifest(
            TripServiceType.ContractCrew,
            [Passenger("M. Beardy", email: "m.beardy@example.ca"), Passenger("R. Ballantyne", phone: "204-555-0101")]);

        var result = await FromTripHandler.Handle(
            new UpsertRidersFromTripCommand(trip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _riders.Riders.Count);
        Assert.All(_riders.Riders, r => Assert.Equal(TripServiceType.ContractCrew, r.ServiceType));
        var beardy = Assert.Single(_riders.Riders, r => r.NormalizedName == "M. BEARDY");
        Assert.Equal("m.beardy@example.ca", beardy.Contact);
        Assert.Equal(1, beardy.TripCount);
        Assert.Equal("TR-4821", beardy.LastTripNumber);
        Assert.Equal(1, _riders.SaveCount);
    }

    [Fact]
    public async Task An_edited_manifest_adds_only_the_new_names()
    {
        var (manifest, trip) = LinkedManifest(
            TripServiceType.ContractCrew, [Passenger("M. Beardy")]);
        Assert.True((await FromTripHandler.Handle(
            new UpsertRidersFromTripCommand(trip.Id), CancellationToken.None)).IsSuccess);

        Assert.True(manifest.Update(
            manifest.TripDate,
            manifest.TripNumber,
            manifest.Route,
            manifest.Direction,
            manifest.Client,
            [Passenger("M. Beardy"), Passenger("J. Colomb")],
            manifest.AllSeatbeltsVerified,
            [],
            manifest.AllCargoSecured,
            ManifestSource.Dispatcher,
            "K. Spence").IsSuccess);

        var result = await FromManifestHandler.Handle(
            new UpsertRidersFromManifestCommand(manifest.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _riders.Riders.Count);
        var beardy = Assert.Single(_riders.Riders, r => r.NormalizedName == "M. BEARDY");
        Assert.Equal(1, beardy.TripCount); // same trip number — the count never inflates on re-save
        Assert.Single(_riders.Riders, r => r.NormalizedName == "J. COLOMB");
    }

    [Fact]
    public async Task A_cargo_trip_upserts_nothing()
    {
        var (_, trip) = LinkedManifest(TripServiceType.Cargo, [Passenger("M. Beardy")]);

        var result = await FromTripHandler.Handle(
            new UpsertRidersFromTripCommand(trip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_riders.Riders);
        Assert.Equal(0, _riders.SaveCount);
    }

    [Fact]
    public async Task A_missing_trip_is_a_success_no_op()
    {
        var result = await FromTripHandler.Handle(
            new UpsertRidersFromTripCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_riders.Riders);
        Assert.Equal(0, _riders.SaveCount);
    }

    [Fact]
    public async Task A_manifest_whose_trip_never_linked_is_a_success_no_op()
    {
        // Trip number matches no trip at all — no trip, no service type, no riders.
        var manifest = TestManifests.Create(tripNumber: "TR-9999", passengers: [Passenger("M. Beardy")]).Value;
        _manifests.Add(manifest);

        var result = await FromManifestHandler.Handle(
            new UpsertRidersFromManifestCommand(manifest.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_riders.Riders);
    }

    [Fact]
    public async Task Duplicate_names_within_one_manifest_produce_one_rider()
    {
        var (_, trip) = LinkedManifest(
            TripServiceType.ContractCrew,
            [Passenger("M. Beardy"), Passenger("  m.   beardy ")]);

        var result = await FromTripHandler.Handle(
            new UpsertRidersFromTripCommand(trip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var rider = Assert.Single(_riders.Riders);
        Assert.Equal("M. BEARDY", rider.NormalizedName);
        Assert.Equal(1, rider.TripCount);
    }

    [Fact]
    public async Task Redelivery_of_the_link_event_is_idempotent()
    {
        var (_, trip) = LinkedManifest(TripServiceType.ContractCrew, [Passenger("M. Beardy")]);
        var command = new UpsertRidersFromTripCommand(trip.Id);
        Assert.True((await FromTripHandler.Handle(command, CancellationToken.None)).IsSuccess);

        var second = await FromTripHandler.Handle(command, CancellationToken.None);

        Assert.True(second.IsSuccess);
        var rider = Assert.Single(_riders.Riders);
        Assert.Equal(1, rider.TripCount);
    }
}
