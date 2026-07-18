using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Tests;

/// <summary>Factory helpers to exercise TripManifest.Create with a valid baseline payload.</summary>
internal static class TestManifests
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>The full canonical §3 checklist, every item Ok.</summary>
    public static List<PreTripChecklistItem> AllOkPreTrip() =>
        ManifestChecklist.PreTripGroups
            .SelectMany(group => group.Value.Select(item => new PreTripChecklistItem
            {
                Group = group.Key,
                Item = item,
                Status = PreTripItemStatus.Ok,
            }))
            .ToList();

    /// <summary>The full canonical §9 checklist, every item confirmed.</summary>
    public static List<PostTripChecklistItem> AllOkPostTrip() =>
        ManifestChecklist.PostTripItems
            .Select(item => new PostTripChecklistItem { Item = item, Ok = true })
            .ToList();

    /// <summary>
    /// A valid App-sourced manifest with every invariant satisfied; override the single
    /// argument a test cares about.
    /// </summary>
    public static Result<TripManifest> Create(
        string tripNumber = "TR-4818",
        string unit = "U-04",
        string driverName = "J. Spence",
        int? odometerStartKm = 118_204,
        int? odometerEndKm = 118_346,
        bool fuelAdded = false,
        decimal? fuelLitres = null,
        IReadOnlyList<PreTripChecklistItem>? preTrip = null,
        IReadOnlyList<ManifestPassenger>? passengers = null,
        IReadOnlyList<ManifestCargoItem>? cargo = null,
        IReadOnlyList<bool>? attestations = null,
        string driverSignatureName = "J. Spence",
        ManifestSource source = ManifestSource.App,
        string? enteredBy = null) =>
        TripManifest.Create(
            TenantId,
            tripDate: new DateOnly(2026, 7, 14),
            tripNumber: tripNumber,
            route: "Black Sturgeon Falls → Thompson",
            direction: TripDirection.Outbound,
            client: null,
            unit: unit,
            driverName: driverName,
            driverLicenceNo: "MB 4218-5590",
            licencePlate: "MB · FRT 771",
            odometerStartKm: odometerStartKm,
            fuelLevel: FuelLevel.ThreeQuarters,
            preTripItems: preTrip ?? AllOkPreTrip(),
            weather: [WeatherCondition.Clear],
            temperatureC: "-21",
            roadConditions: [RoadCondition.SnowCovered],
            visibility: VisibilityLevel.Good,
            roadAdvisories: null,
            passengers: passengers ?? [new ManifestPassenger { Name = "M. Beardy", IdVerified = true, BoardedOn = true, BoardedOff = true }],
            allSeatbeltsVerified: true,
            cargo: cargo ?? [],
            allCargoSecured: CargoSecuredStatus.NotApplicable,
            issues: [],
            noIssues: true,
            departureTime: "07:45",
            arrivalTime: "10:20",
            odometerEndKm: odometerEndKm,
            totalKm: 142,
            fuelAdded: fuelAdded,
            fuelLitres: fuelLitres,
            fuelCostCad: null,
            postTripItems: AllOkPostTrip(),
            attestations: attestations ?? [true, true, true, true, true],
            driverSignatureName: driverSignatureName,
            certifiedAt: DateTimeOffset.UtcNow,
            source: source,
            enteredBy: enteredBy);
}
