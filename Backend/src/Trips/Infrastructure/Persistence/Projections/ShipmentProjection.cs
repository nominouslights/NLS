using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Shipment"/> into <c>trips.rm_shipments</c> <b>and</b>
/// <c>trips.rm_shipment_legs</c> — one projection writing two tables, because the leg rows carry
/// a denormalised copy of the shipment fields a trip's cargo section renders. Rewriting all of a
/// shipment's legs on every event is what stops those copies drifting.
///
/// Every query uses <c>IgnoreQueryFilters()</c>: the projection worker runs with no ambient
/// tenant, so the tenant query filter would compare against a null TenantId and match nothing.
/// Spanning tenants is intentional and is what the worker's pinned <c>app.is_system</c> session
/// authorises. Nothing is saved here — the worker commits with the checkpoint in one save.
/// </summary>
internal sealed class ShipmentProjection : IProjection<TripsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Shipment));

    public async Task ApplyAsync(TripsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var shipment = await context.Shipments
            .IgnoreQueryFilters()
            .Include(s => s.Legs)
            .FirstOrDefaultAsync(s => s.Id == aggregateId, cancellationToken);

        var row = await context.ShipmentReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        var legRows = await context.ShipmentLegReadModels
            .IgnoreQueryFilters()
            .Where(r => r.ShipmentId == aggregateId)
            .ToListAsync(cancellationToken);

        if (shipment is null)
        {
            if (row is not null)
            {
                context.ShipmentReadModels.Remove(row);
            }

            context.ShipmentLegReadModels.RemoveRange(legRows);
            return;
        }

        if (row is null)
        {
            row = new ShipmentReadModel();
            Map(shipment, row);
            context.ShipmentReadModels.Add(row);
        }
        else
        {
            Map(shipment, row);
        }

        SyncLegs(context, shipment, legRows);
    }

    public async Task RebuildAllAsync(TripsDbContext context, CancellationToken cancellationToken)
    {
        var shipments = await context.Shipments
            .IgnoreQueryFilters()
            .Include(s => s.Legs)
            .ToListAsync(cancellationToken);

        var rows = await context.ShipmentReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var legRows = await context.ShipmentLegReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var legsByShipment = legRows.GroupBy(l => l.ShipmentId).ToDictionary(g => g.Key, g => g.ToList());
        var seen = new HashSet<Guid>();

        foreach (var shipment in shipments)
        {
            seen.Add(shipment.Id);

            if (rowsById.TryGetValue(shipment.Id, out var existing))
            {
                Map(shipment, existing);
            }
            else
            {
                var fresh = new ShipmentReadModel();
                Map(shipment, fresh);
                context.ShipmentReadModels.Add(fresh);
            }

            SyncLegs(
                context,
                shipment,
                legsByShipment.TryGetValue(shipment.Id, out var existingLegs) ? existingLegs : []);
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.ShipmentReadModels.Remove(row);
            }
        }

        foreach (var (shipmentId, orphans) in legsByShipment)
        {
            if (!seen.Contains(shipmentId))
            {
                context.ShipmentLegReadModels.RemoveRange(orphans);
            }
        }
    }

    /// <summary>
    /// Upserts this shipment's leg rows and deletes any that no longer exist — a removed leg (or
    /// one released by a cancelled trip) has to disappear from the carrying trip's cargo section
    /// immediately, or a dispatcher loads freight that is not booked on the run.
    /// </summary>
    private static void SyncLegs(
        TripsDbContext context,
        Shipment shipment,
        List<ShipmentLegReadModel> existingRows)
    {
        var byId = existingRows.ToDictionary(r => r.Id);
        var ordered = shipment.Legs.OrderBy(l => l.Sequence).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var leg = ordered[index];
            var isFinal = index == ordered.Count - 1;
            var onward = isFinal ? null : ordered[index + 1].TripNumber;

            if (byId.TryGetValue(leg.Id, out var existing))
            {
                MapLeg(shipment, leg, isFinal, onward, existing);
                byId.Remove(leg.Id);
            }
            else
            {
                var fresh = new ShipmentLegReadModel();
                MapLeg(shipment, leg, isFinal, onward, fresh);
                context.ShipmentLegReadModels.Add(fresh);
            }
        }

        context.ShipmentLegReadModels.RemoveRange(byId.Values);
    }

    private static void Map(Shipment source, ShipmentReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;

        row.ShipmentNumber = source.ShipmentNumber;
        row.Status = source.Status.ToString();
        row.Kind = source.Kind.ToString();

        row.ClientId = source.ClientId;
        row.ClientName = source.ClientName;
        row.PoNumber = source.PoNumber;

        row.ConsignorName = source.ConsignorName;
        row.ConsignorContact = source.ConsignorContact;
        row.ConsigneeName = source.ConsigneeName;
        row.ConsigneeContact = source.ConsigneeContact;

        row.OriginStopId = source.OriginStopId;
        row.OriginName = source.OriginName;
        row.DestinationStopId = source.DestinationStopId;
        row.DestinationName = source.DestinationName;

        row.Description = source.Description;
        row.Pieces = source.Pieces;
        row.WeightKg = source.WeightKg;
        row.LengthCm = source.LengthCm;
        row.WidthCm = source.WidthCm;
        row.HeightCm = source.HeightCm;
        row.Hazmat = source.Hazmat;
        row.DeclaredValueCad = source.DeclaredValueCad;
        row.SpecialHandling = source.SpecialHandling;
        row.Secured = source.Secured;

        row.ChargeCad = source.ChargeCad;
        row.PaymentMethod = source.PaymentMethod?.ToString();
        row.PaymentCollectedAtUtc = source.PaymentCollectedAtUtc;

        row.ReadyDate = source.ReadyDate;
        row.RequiredByDate = source.RequiredByDate;

        var current = source.CurrentLeg;
        row.LegCount = source.Legs.Count;
        row.CurrentTripId = current?.TripId;
        row.CurrentTripNumber = current?.TripNumber;
        row.LastServiceDate = source.Legs.Count == 0
            ? null
            : source.Legs.Max(l => l.TripServiceDate);
        row.AwaitingTransfer = source.IsAwaitingTransfer;

        row.DeliveredAtUtc = source.DeliveredAtUtc;
        row.ReceivedBy = source.ReceivedBy;
        row.DeliveryNote = source.DeliveryNote;

        row.CancelledReason = source.CancelledReason;
        row.WrittenOffReason = source.WrittenOffReason;

        row.Source = source.Source.ToString();
        row.EnteredBy = source.EnteredBy;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }

    private static void MapLeg(
        Shipment shipment,
        ShipmentLeg leg,
        bool isFinalLeg,
        string? onwardTripNumber,
        ShipmentLegReadModel row)
    {
        row.Id = leg.Id;
        row.TenantId = leg.TenantId;
        row.ShipmentId = leg.ShipmentId;
        row.Sequence = leg.Sequence;

        row.TripId = leg.TripId;
        row.TripNumber = leg.TripNumber;
        row.TripServiceDate = leg.TripServiceDate;

        row.FromStopId = leg.FromStopId;
        row.FromName = leg.FromName;
        row.ToStopId = leg.ToStopId;
        row.ToName = leg.ToName;

        row.Status = leg.Status.ToString();
        row.AssignedAtUtc = leg.AssignedAtUtc;
        row.PickedUpAtUtc = leg.PickedUpAtUtc;
        row.PickedUpBy = leg.PickedUpBy;
        row.DroppedAtUtc = leg.DroppedAtUtc;
        row.DroppedBy = leg.DroppedBy;

        row.IsFinalLeg = isFinalLeg;
        row.OnwardTripNumber = onwardTripNumber;

        // Denormalised shipment snapshot — the manifest's §6 reads these directly.
        row.ShipmentNumber = shipment.ShipmentNumber;
        row.ShipmentStatus = shipment.Status.ToString();
        row.Kind = shipment.Kind.ToString();
        row.Description = shipment.Description;
        row.ClientId = shipment.ClientId;
        row.ClientName = shipment.ClientName;
        row.ConsigneeName = shipment.ConsigneeName;
        row.Pieces = shipment.Pieces;
        row.WeightKg = shipment.WeightKg;
        row.ChargeCad = shipment.ChargeCad;
        row.Hazmat = shipment.Hazmat;
        row.Secured = shipment.Secured;

        row.Version = shipment.Version;
    }
}
