using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Manifests;
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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
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
