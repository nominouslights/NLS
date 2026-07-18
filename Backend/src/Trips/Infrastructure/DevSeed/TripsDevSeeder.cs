using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Infrastructure.Persistence;

namespace NorthernLink.Trips.Infrastructure.DevSeed;

/// <summary>
/// Development-only seed: one completed App-sourced manifest for the Dispatcher's
/// U-04 demo vehicle, so the Trips screen's PRINT TRIP MANIFEST flow works out of the
/// box. Includes one Minor pre-trip FAIL echoed in the issues log, so the filled PDF
/// exercises §3 severity rendering and §7 in one record — and saving it publishes
/// trips.trip-manifest-completed, which materializes the two Fleet inspection rows.
/// Idempotent — skips when any manifest already exists.
/// </summary>
public static class TripsDevSeeder
{
    public static async Task SeedAsync(TripsDbContext context, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (await context.Manifests.AnyAsync(cancellationToken))
        {
            return;
        }

        var preTrip = ManifestChecklist.PreTripGroups
            .SelectMany(group => group.Value.Select(item =>
                item == "Wipers & washer fluid"
                    ? new PreTripChecklistItem
                    {
                        Group = group.Key,
                        Item = item,
                        Status = PreTripItemStatus.Fail,
                        Severity = DefectSeverity.Minor,
                        Note = "Streaking, replace blade",
                    }
                    : new PreTripChecklistItem
                    {
                        Group = group.Key,
                        Item = item,
                        Status = PreTripItemStatus.Ok,
                    }))
            .ToList();

        string[] passengerNames =
        [
            "M. Beardy", "T. Halcrow", "L. Spence", "R. North", "C. Wood", "D. Monias",
        ];
        var passengers = passengerNames
            .Select(name => new ManifestPassenger
            {
                Name = name,
                Contact = null,
                Pickup = "Black Sturgeon Falls",
                Dropoff = "Thompson",
                IdVerified = true,
                BoardedOn = true,
                BoardedOff = true,
            })
            .ToList();

        var cargo = new List<ManifestCargoItem>
        {
            new()
            {
                Description = "Grocery totes (4)",
                OwnerRecipient = "M. Beardy",
                WeightKg = 38.5m,
                ChargeCad = 40.00m,
                Hazmat = false,
                Secured = true,
            },
            new()
            {
                Description = "Parcel — hardware order",
                OwnerRecipient = "Band office",
                WeightKg = 12.0m,
                ChargeCad = 23.80m,
                Hazmat = false,
                Secured = true,
            },
        };

        var postTrip = ManifestChecklist.PostTripItems
            .Select(item => new PostTripChecklistItem { Item = item, Ok = true })
            .ToList();

        var manifest = TripManifest.Create(
            tenantId,
            tripDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
            tripNumber: "TR-4818",
            route: "Black Sturgeon Falls → Thompson",
            direction: TripDirection.Outbound,
            client: null,
            unit: "U-04",
            driverName: "J. Spence",
            driverLicenceNo: "MB 4218-5590",
            licencePlate: "MB · FRT 771",
            odometerStartKm: 118_204,
            fuelLevel: FuelLevel.ThreeQuarters,
            preTripItems: preTrip,
            weather: [WeatherCondition.Clear, WeatherCondition.ExtremeCold],
            temperatureC: "-21",
            roadConditions: [RoadCondition.SnowCovered],
            visibility: VisibilityLevel.Good,
            roadAdvisories: null,
            passengers: passengers,
            allSeatbeltsVerified: true,
            cargo: cargo,
            allCargoSecured: CargoSecuredStatus.Yes,
            issues: ["Wiper blades streaking — replacement blade needed (Minor)"],
            noIssues: false,
            departureTime: "07:45",
            arrivalTime: "10:20",
            odometerEndKm: 118_346,
            totalKm: 142,
            fuelAdded: true,
            fuelLitres: 38m,
            fuelCostCad: 63.80m,
            postTripItems: postTrip,
            attestations: [true, true, true, true, true],
            driverSignatureName: "J. Spence",
            certifiedAt: DateTimeOffset.UtcNow,
            source: ManifestSource.App,
            enteredBy: null).Value;

        context.Manifests.Add(manifest);
        await context.SaveChangesAsync(cancellationToken);
    }
}
