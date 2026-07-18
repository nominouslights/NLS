using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests;

/// <summary>Maps the TripManifest aggregate to the module's public response contract.</summary>
public static class TripManifestResponseMapper
{
    public static TripManifestResponse ToResponse(TripManifest manifest) => new(
        manifest.Id,
        manifest.TripDate,
        manifest.TripNumber,
        manifest.Route,
        manifest.Direction?.ToString(),
        manifest.Client,
        manifest.Unit,
        manifest.DriverName,
        manifest.DriverLicenceNo,
        manifest.LicencePlate,
        manifest.OdometerStartKm,
        manifest.FuelLevel?.ToString(),
        manifest.PreTripItems.Select(item => new PreTripItemResponse(
            item.Group,
            item.Item,
            item.Status.ToString(),
            item.Severity?.ToString(),
            item.Note)).ToList(),
        manifest.Weather.Select(w => w.ToString()).ToList(),
        manifest.TemperatureC,
        manifest.RoadConditions.Select(r => r.ToString()).ToList(),
        manifest.Visibility?.ToString(),
        manifest.RoadAdvisories,
        manifest.Passengers.Select(p => new PassengerResponse(
            p.Name,
            p.Contact,
            p.Pickup,
            p.Dropoff,
            p.IdVerified,
            p.BoardedOn,
            p.BoardedOff)).ToList(),
        manifest.AllSeatbeltsVerified,
        manifest.Cargo.Select(c => new CargoItemResponse(
            c.Description,
            c.OwnerRecipient,
            c.WeightKg,
            c.ChargeCad,
            c.Hazmat,
            c.Secured)).ToList(),
        manifest.AllCargoSecured?.ToString(),
        manifest.Issues,
        manifest.NoIssues,
        manifest.DepartureTime,
        manifest.ArrivalTime,
        manifest.OdometerEndKm,
        manifest.TotalKm,
        manifest.FuelAdded,
        manifest.FuelLitres,
        manifest.FuelCostCad,
        manifest.PostTripItems.Select(item => new PostTripItemResponse(item.Item, item.Ok)).ToList(),
        manifest.Attestations,
        manifest.DriverSignatureName,
        manifest.CertifiedAt,
        manifest.Source.ToString(),
        manifest.EnteredBy,
        manifest.EnteredAt,
        manifest.CreatedAtUtc);
}
