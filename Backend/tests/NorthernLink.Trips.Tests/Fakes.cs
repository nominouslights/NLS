using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Riders;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Stops;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Tests;

/// <summary>In-memory fakes for Application-layer handler tests (no EF, no Postgres).</summary>
internal sealed class FakeTripRepository : ITripRepository
{
    public List<Trip> Trips { get; } = [];
    public int SaveCount { get; private set; }

    public void Add(Trip trip) => Trips.Add(trip);

    public Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Trips.FirstOrDefault(t => t.Id == tripId));

    public Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Trips.FirstOrDefault(t => t.TripNumber == tripNumber));

    public Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(
        string roundTripKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trip>>(Trips.Where(t => t.RoundTripKey == roundTripKey).ToList());

    public Task<IReadOnlyList<Trip>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default)
    {
        var wanted = tripIds.ToHashSet();
        IReadOnlyList<Trip> matches = Trips
            .Where(t => t.TenantId == tenantId && wanted.Contains(t.Id))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory trip-billing replica. Mirrors the real reconcile's semantics where tests care:
/// upsert-by-trip, delete-on-release, and the per-trip high-water mark on UpdatedAtUtc.
/// </summary>
internal sealed class FakeTripBillingRepository : ITripBillingRepository
{
    public List<TripBilling> Rows { get; } = [];
    public int ReconcileCount { get; private set; }

    public Task<IReadOnlyList<TripBilling>> GetByInvoiceAsync(
        Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripBilling>>(
            Rows.Where(b => b.TenantId == tenantId && b.InvoiceId == invoiceId).ToList());

    public Task<IReadOnlyList<TripBilling>> GetByTripIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> tripIds, CancellationToken cancellationToken = default)
    {
        var wanted = tripIds.ToHashSet();
        IReadOnlyList<TripBilling> matches = Rows
            .Where(b => b.TenantId == tenantId && wanted.Contains(b.TripId))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task ReconcileAsync(
        Guid tenantId,
        Guid invoiceId,
        IReadOnlyList<TripBilling> claimed,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ReconcileCount++;

        var claimedTripIds = claimed.Select(b => b.TripId).ToHashSet();
        foreach (var row in claimed)
        {
            var current = Rows.FirstOrDefault(b => b.TripId == row.TripId && b.TenantId == tenantId);
            if (current is not null && current.UpdatedAtUtc > occurredAtUtc)
            {
                continue;
            }

            if (current is not null)
            {
                Rows.Remove(current);
            }

            Rows.Add(row);
        }

        Rows.RemoveAll(b =>
            b.TenantId == tenantId
            && b.InvoiceId == invoiceId
            && !claimedTripIds.Contains(b.TripId)
            && b.UpdatedAtUtc <= occurredAtUtc);

        return Task.CompletedTask;
    }
}

internal sealed class FakeDriverLookupRepository : IDriverLookupRepository
{
    public List<DriverLookup> Drivers { get; } = [];

    public Task<DriverLookup?> GetAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Drivers.FirstOrDefault(d => d.DriverId == driverId));

    public Task UpsertAsync(DriverLookup driver, CancellationToken cancellationToken = default)
    {
        Drivers.RemoveAll(d => d.DriverId == driver.DriverId);
        Drivers.Add(driver);
        return Task.CompletedTask;
    }
}

internal sealed class FakeVehicleLookupRepository : IVehicleLookupRepository
{
    public List<VehicleLookup> Vehicles { get; } = [];

    public Task<VehicleLookup?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vehicles.FirstOrDefault(v => v.VehicleId == vehicleId));

    public Task UpsertAsync(VehicleLookup vehicle, CancellationToken cancellationToken = default)
    {
        Vehicles.RemoveAll(v => v.VehicleId == vehicle.VehicleId);
        Vehicles.Add(vehicle);
        return Task.CompletedTask;
    }
}

internal sealed class FakeStopRepository : IStopRepository
{
    public List<Stop> Stops { get; } = [];
    public int SaveCount { get; private set; }

    public void Add(Stop stop) => Stops.Add(stop);

    public Task<Stop?> GetByIdAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Stops.FirstOrDefault(s => s.Id == stopId));

    public Task<IReadOnlyList<Stop>> GetByIdsAsync(
        IReadOnlyCollection<Guid> stopIds, CancellationToken cancellationToken = default)
    {
        var wanted = stopIds.ToHashSet();
        IReadOnlyList<Stop> matches = Stops.Where(s => wanted.Contains(s.Id)).ToList();
        return Task.FromResult(matches);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeRouteRepository : IRouteRepository
{
    /// <summary>The last route handed to <see cref="Add"/> — captured so snapshot tests can inspect its Stops.</summary>
    public Route? Added { get; private set; }
    public int SaveCount { get; private set; }

    public void Add(Route route) => Added = route;

    public Task<Route?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Added?.Id == routeId ? Added : null);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeTripActivityReadService : ITripActivityReadService
{
    /// <summary>Rows returned verbatim (the handler applies its own ordering + payload mining).</summary>
    public List<TripActivityJournalEntry> Entries { get; } = [];

    public Guid? RequestedTripId { get; private set; }
    public Guid? RequestedManifestId { get; private set; }

    public Task<IReadOnlyList<TripActivityJournalEntry>> GetJournalEntriesAsync(
        Guid tripId, Guid? manifestId, CancellationToken cancellationToken = default)
    {
        RequestedTripId = tripId;
        RequestedManifestId = manifestId;
        return Task.FromResult<IReadOnlyList<TripActivityJournalEntry>>(Entries);
    }
}

internal sealed class FakeRiderRepository : IRiderRepository
{
    public List<Rider> Riders { get; } = [];
    public int SaveCount { get; private set; }

    public void Add(Rider rider) => Riders.Add(rider);

    public Task<Rider?> GetByIdAsync(Guid riderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Riders.FirstOrDefault(r => r.Id == riderId));

    public Task<Rider?> GetByKeyAsync(
        TripServiceType serviceType, string normalizedName, CancellationToken cancellationToken = default) =>
        Task.FromResult(Riders.FirstOrDefault(r =>
            r.ServiceType == serviceType && r.NormalizedName == normalizedName));

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeTripManifestRepository : ITripManifestRepository
{
    public List<TripManifest> Manifests { get; } = [];
    public int SaveCount { get; private set; }

    public void Add(TripManifest manifest) => Manifests.Add(manifest);

    public Task<TripManifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Manifests.FirstOrDefault(m => m.Id == manifestId));

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
