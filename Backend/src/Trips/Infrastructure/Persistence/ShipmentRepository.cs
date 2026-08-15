using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Write-side repository over <see cref="TripsDbContext"/> (tenant-filtered). Every load that
/// returns an aggregate includes its legs: routing is part of the aggregate's invariants, and a
/// Shipment with an unloaded leg collection would silently resequence to nothing on save.
/// </summary>
internal sealed class ShipmentRepository(TripsDbContext context) : IShipmentRepository
{
    public void Add(Shipment shipment) => context.Shipments.Add(shipment);

    public Task<Shipment?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken = default) =>
        context.Shipments
            .Include(s => s.Legs)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);

    public Task<Shipment?> GetByNumberAsync(string shipmentNumber, CancellationToken cancellationToken = default) =>
        context.Shipments
            .Include(s => s.Legs)
            .FirstOrDefaultAsync(s => s.ShipmentNumber == shipmentNumber, cancellationToken);

    public async Task<IReadOnlyList<Shipment>> GetByIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds,
        CancellationToken cancellationToken = default)
    {
        if (shipmentIds.Count == 0)
        {
            return [];
        }

        return await context.Shipments
            .Include(s => s.Legs)
            .Where(s => shipmentIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> GetForTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default) =>
        await context.Shipments
            .Include(s => s.Legs)
            .Where(s => s.Legs.Any(l => l.TripId == tripId))
            .ToListAsync(cancellationToken);

    public Task<int> CountForTripAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        context.ShipmentLegs.CountAsync(l => l.TripId == tripId, cancellationToken);

    public async Task<IReadOnlyList<Shipment>> GetByIdsForTenantAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> shipmentIds,
        CancellationToken cancellationToken = default)
    {
        if (shipmentIds.Count == 0)
        {
            return [];
        }

        return await context.Shipments
            .IgnoreQueryFilters()
            .Include(s => s.Legs)
            .Where(s => s.TenantId == tenantId && shipmentIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
