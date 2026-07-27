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
        manifest.Passengers.Select(p => new PassengerResponse(
            p.Name,
            p.Contact,
            p.PickupStopId,
            p.PickupStopName,
            p.DropoffStopId,
            p.DropoffStopName,
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
        manifest.Source.ToString(),
        manifest.EnteredBy,
        manifest.EnteredAt,
        manifest.CreatedAtUtc);
}
