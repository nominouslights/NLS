using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Read side — queries the projection read model (<c>trips.rm_trip_manifests</c>) and maps
/// to the public contract (the jsonb row collections materialize with the read model).
/// </summary>
internal sealed class TripManifestReadService(TripsDbContext context) : ITripManifestReadService
{
    public async Task<IReadOnlyList<TripManifestResponse>> GetManifestsAsync(
        string? tripNumber = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.ManifestReadModels.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(tripNumber))
        {
            query = query.Where(m => m.TripNumber == tripNumber.Trim());
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
        m.Passengers.Select(p => new PassengerResponse(
            p.Name,
            p.Email,
            p.Phone,
            p.PickupStopId,
            p.PickupStopName,
            p.DropoffStopId,
            p.DropoffStopName,
            p.IdVerified,
            p.BoardedOn,
            p.BoardedOff,
            p.FareAmountCad,
            p.FarePaymentMethod?.ToString(),
            p.FarePaidAtUtc)).ToList(),
        m.AllSeatbeltsVerified,
        m.Cargo.Select(c => new CargoItemResponse(
            c.Description,
            c.OwnerRecipient,
            c.WeightKg,
            c.ChargeCad,
            c.Hazmat,
            c.Secured)).ToList(),
        m.AllCargoSecured,
        m.Source,
        m.EnteredBy,
        m.EnteredAt,
        m.CreatedAtUtc,
        // Computed off the deserialized rows rather than stored — the read model already
        // carries the full passengers jsonb, so a stored rollup could only ever drift from it.
        FaresCollectedCad(m.Passengers),
        m.Passengers.Count(p => p.FarePaymentMethod
            is FarePaymentMethod.Cash or FarePaymentMethod.Online),
        m.Passengers.Count(p => p.FarePaymentMethod is FarePaymentMethod.Waived));

    private static decimal FaresCollectedCad(IEnumerable<ManifestPassenger> passengers) =>
        Math.Round(
            passengers
                .Where(p => p.FarePaymentMethod is FarePaymentMethod.Cash or FarePaymentMethod.Online)
                .Sum(p => p.FareAmountCad ?? 0m),
            2);
}
