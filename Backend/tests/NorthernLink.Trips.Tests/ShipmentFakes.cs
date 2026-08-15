using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Tests;

/// <summary>In-memory fakes for the shipment handler tests (no EF, no Postgres).</summary>
internal sealed class FakeShipmentRepository : IShipmentRepository
{
    public List<Shipment> Shipments { get; } = [];
    public int SaveCount { get; private set; }

    public void Add(Shipment shipment) => Shipments.Add(shipment);

    public Task<Shipment?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Shipments.FirstOrDefault(s => s.Id == shipmentId));

    public Task<Shipment?> GetByNumberAsync(string shipmentNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Shipments.FirstOrDefault(s => s.ShipmentNumber == shipmentNumber));

    public Task<IReadOnlyList<Shipment>> GetByIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Shipment>>([.. Shipments.Where(s => shipmentIds.Contains(s.Id))]);

    public Task<IReadOnlyList<Shipment>> GetForTripAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Shipment>>(
            [.. Shipments.Where(s => s.Legs.Any(l => l.TripId == tripId))]);

    public Task<int> CountForTripAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Shipments.Sum(s => s.Legs.Count(l => l.TripId == tripId)));

    public Task<IReadOnlyList<Shipment>> GetByIdsForTenantAsync(
        Guid tenantId, IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Shipment>>(
            [.. Shipments.Where(s => s.TenantId == tenantId && shipmentIds.Contains(s.Id))]);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeClientLookupRepository : IClientLookupRepository
{
    public List<ClientLookup> Clients { get; } = [];

    public Task<ClientLookup?> GetAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Clients.FirstOrDefault(c => c.ClientId == clientId));

    public Task UpsertAsync(ClientLookup client, CancellationToken cancellationToken = default)
    {
        Clients.RemoveAll(c => c.ClientId == client.ClientId);
        Clients.Add(client);
        return Task.CompletedTask;
    }
}

internal sealed class FakeShipmentNumberGenerator : IShipmentNumberGenerator
{
    private int _next = 1000;

    public Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult($"SH-{++_next}");
}
