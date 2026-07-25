using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Read side — queries the materialized-view-backed read model through the tenant wrapper
/// view <c>trips.v_trip_manifests</c> and maps to the public contract (the jsonb row
/// collections materialize with the read model).
/// </summary>
internal sealed class TripManifestReadService(TripsDbContext context) : ITripManifestReadService
{
    public async Task<IReadOnlyList<TripManifestResponse>> GetManifestsAsync(
        string? tripNumber = null,
        string? unit = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.ManifestReadModels.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(tripNumber))
        {
            query = query.Where(m => m.TripNumber == tripNumber.Trim());
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            query = query.Where(m => m.Unit == unit.Trim());
        }

        var manifests = await query
            .OrderByDescending(m => m.TripDate)
            .ThenByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return manifests.Select(ToResponse).ToList();
    }

    public async Task<TripManifestResponse?> GetManifestAsync(
        Guid manifestId,
        CancellationToken cancellationToken = default)
    {
        var manifest = await context.ManifestReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == manifestId, cancellationToken);

        return manifest is null ? null : ToResponse(manifest);
    }

    private static TripManifestResponse ToResponse(TripManifestReadModel m) => new(
        m.Id,
        m.TripDate,
        m.TripNumber,
        m.Route,
        m.Direction,
        m.Client,
        m.Unit,
        m.DriverName,
        m.DriverLicenceNo,
        m.LicencePlate,
        m.OdometerStartKm,
        m.FuelLevel,
        m.PreTripItems.Select(item => new PreTripItemResponse(
            item.Group,
            item.Item,
            item.Status.ToString(),
            item.Severity?.ToString(),
            item.Note)).ToList(),
        m.Weather,
        m.TemperatureC,
        m.RoadConditions,
        m.Visibility,
        m.RoadAdvisories,
        m.Passengers.Select(p => new PassengerResponse(
            p.Name,
            p.Contact,
            p.Pickup,
            p.Dropoff,
            p.IdVerified,
            p.BoardedOn,
            p.BoardedOff)).ToList(),
        m.AllSeatbeltsVerified,
        m.Cargo.Select(c => new CargoItemResponse(
            c.Description,
            c.OwnerRecipient,
            c.WeightKg,
            c.ChargeCad,
            c.Hazmat,
            c.Secured)).ToList(),
        m.AllCargoSecured,
        m.Issues,
        m.NoIssues,
        m.DepartureTime,
        m.ArrivalTime,
        m.OdometerEndKm,
        m.TotalKm,
        m.FuelAdded,
        m.FuelLitres,
        m.FuelCostCad,
        m.PostTripItems.Select(item => new PostTripItemResponse(item.Item, item.Ok)).ToList(),
        m.Attestations,
        m.DriverSignatureName,
        m.CertifiedAt,
        m.Source,
        m.EnteredBy,
        m.EnteredAt,
        m.CreatedAtUtc);
}
