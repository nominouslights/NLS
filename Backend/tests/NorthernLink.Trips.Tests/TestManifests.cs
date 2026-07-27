using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Tests;

/// <summary>Factory helpers to exercise TripManifest.Create with a valid baseline payload.</summary>
internal static class TestManifests
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>One boarded passenger, referencing a route stop by snapshot.</summary>
    public static List<ManifestPassenger> OnePassenger() =>
    [
        new ManifestPassenger
        {
            Name = "M. Beardy",
            PickupStopId = Guid.Parse("00000000-0000-0000-0000-0000000000a1"),
            PickupStopName = "Black Sturgeon Falls",
            DropoffStopId = Guid.Parse("00000000-0000-0000-0000-0000000000a2"),
            DropoffStopName = "Thompson",
            IdVerified = true,
            BoardedOn = true,
            BoardedOff = true,
        },
    ];

    /// <summary>
    /// A valid App-sourced manifest with a single passenger and no cargo; override the
    /// single argument a test cares about.
    /// </summary>
    public static Result<TripManifest> Create(
        string tripNumber = "TR-4818",
        IReadOnlyList<ManifestPassenger>? passengers = null,
        bool allSeatbeltsVerified = true,
        IReadOnlyList<ManifestCargoItem>? cargo = null,
        CargoSecuredStatus? allCargoSecured = CargoSecuredStatus.NotApplicable,
        ManifestSource source = ManifestSource.App,
        string? enteredBy = null) =>
        TripManifest.Create(
            TenantId,
            tripDate: new DateOnly(2026, 7, 14),
            tripNumber: tripNumber,
            route: "Black Sturgeon Falls → Thompson",
            direction: TripDirection.Outbound,
            client: null,
            passengers: passengers ?? OnePassenger(),
            allSeatbeltsVerified: allSeatbeltsVerified,
            cargo: cargo ?? [],
            allCargoSecured: allCargoSecured,
            source: source,
            enteredBy: enteredBy);
}
