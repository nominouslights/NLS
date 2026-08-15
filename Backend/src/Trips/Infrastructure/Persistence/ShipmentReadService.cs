using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Shipments;
using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Read-side queries over the shipment projections. The list joins <c>rm_trips</c> only to carry
/// the <em>trip's</em> client onto each leg — deliberately a read-time join rather than a
/// denormalised column, because a trip's client can change after the shipment was last projected
/// and a stale copy here would misreport who a parcel is riding with.
/// </summary>
internal sealed class ShipmentReadService(TripsDbContext context) : IShipmentReadService
{
    public async Task<ShipmentPageResponse> GetShipmentsAsync(
        ShipmentListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = context.ShipmentReadModels.AsNoTracking().AsQueryable();

        if (filter.Status is { } status)
        {
            query = query.Where(s => s.Status == status);
        }

        if (filter.Kind is { } kind)
        {
            query = query.Where(s => s.Kind == kind);
        }

        if (filter.UnassignedOnly)
        {
            query = query.Where(s => s.LegCount == 0 && s.Status == nameof(ShipmentStatus.Registered));
        }

        if (filter.ClientId is { } clientId)
        {
            query = query.Where(s => s.ClientId == clientId);
        }

        if (filter.ClientlessOnly)
        {
            query = query.Where(s => s.ClientId == null);
        }

        if (filter.AwaitingDelivery)
        {
            query = query.Where(s =>
                s.Status == nameof(ShipmentStatus.Assigned) || s.Status == nameof(ShipmentStatus.InTransit));
        }

        if (filter.AwaitingTransferOnly)
        {
            query = query.Where(s => s.AwaitingTransfer);
        }

        if (filter.BillableOnly)
        {
            query = query.Where(s => s.Status == nameof(ShipmentStatus.ReadyForBilling));
        }

        if (filter.TripId is { } tripId)
        {
            query = query.Where(s => context.ShipmentLegReadModels
                .Any(l => l.ShipmentId == s.Id && l.TripId == tripId));
        }

        if (filter.TripNumber is { } tripNumber)
        {
            query = query.Where(s => context.ShipmentLegReadModels
                .Any(l => l.ShipmentId == s.Id && l.TripNumber == tripNumber));
        }

        // Date window over the most meaningful date the shipment has: when it last moved, else
        // when it was due, else when it was booked in.
        if (filter.From is { } from)
        {
            query = query.Where(s =>
                (s.LastServiceDate ?? s.ReadyDate ?? DateOnly.FromDateTime(s.CreatedAtUtc.UtcDateTime)) >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(s =>
                (s.LastServiceDate ?? s.ReadyDate ?? DateOnly.FromDateTime(s.CreatedAtUtc.UtcDateTime)) <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = $"%{filter.Query.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.ShipmentNumber, term)
                || EF.Functions.ILike(s.Description, term)
                || (s.ConsigneeName != null && EF.Functions.ILike(s.ConsigneeName, term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = query
            .OrderByDescending(s => s.LastServiceDate ?? s.ReadyDate)
            .ThenBy(s => s.ShipmentNumber);

        // Clamp rather than reject, verbatim from the trips list: a caller asking for too much
        // gets the maximum, not a 400.
        var page = filter.Page is > 0 ? filter.Page.Value : 1;
        var pageSize = filter.PageSize is > 0
            ? Math.Min(filter.PageSize.Value, ShipmentListFilter.MaxPageSize)
            : ShipmentListFilter.MaxPageSize;

        if (filter.Page is not null || filter.PageSize is not null)
        {
            query = query.Skip((page - 1) * pageSize).Take(pageSize);
        }

        var rows = await query.ToListAsync(cancellationToken);
        var items = await ToResponsesAsync(rows, cancellationToken);

        return new ShipmentPageResponse(items, page, pageSize, totalCount);
    }

    public async Task<ShipmentResponse?> GetByIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        var row = await context.ShipmentReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);

        if (row is null)
        {
            return null;
        }

        var responses = await ToResponsesAsync([row], cancellationToken);
        return responses.Count == 0 ? null : responses[0];
    }

    public async Task<IReadOnlyList<TripCargoResponse>> GetCargoForTripNumberAsync(
        string tripNumber,
        CancellationToken cancellationToken = default)
    {
        var legs = await context.ShipmentLegReadModels
            .AsNoTracking()
            .Where(l => l.TripNumber == tripNumber)
            .OrderBy(l => l.ShipmentNumber)
            .ToListAsync(cancellationToken);

        return [.. legs.Select(ToCargo)];
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<TripCargoResponse>>> GetCargoForTripNumbersAsync(
        IReadOnlyCollection<string> tripNumbers,
        CancellationToken cancellationToken = default)
    {
        if (tripNumbers.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<TripCargoResponse>>();
        }

        var legs = await context.ShipmentLegReadModels
            .AsNoTracking()
            .Where(l => tripNumbers.Contains(l.TripNumber))
            .OrderBy(l => l.ShipmentNumber)
            .ToListAsync(cancellationToken);

        return legs
            .GroupBy(l => l.TripNumber)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TripCargoResponse>)[.. group.Select(ToCargo)]);
    }

    /// <summary>
    /// Materializes rows into responses, attaching each leg's trip client in one extra query.
    /// Keeping it to one query is the reason this exists rather than a per-row join.
    /// </summary>
    private async Task<IReadOnlyList<ShipmentResponse>> ToResponsesAsync(
        List<ShipmentReadModel> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.Id).ToList();

        var legs = await context.ShipmentLegReadModels
            .AsNoTracking()
            .Where(l => ids.Contains(l.ShipmentId))
            .OrderBy(l => l.Sequence)
            .ToListAsync(cancellationToken);

        var tripIds = legs.Select(l => l.TripId).Distinct().ToList();
        var tripClients = await context.TripReadModels
            .AsNoTracking()
            .Where(t => tripIds.Contains(t.Id))
            .Select(t => new { t.Id, t.ClientId, t.ClientName })
            .ToListAsync(cancellationToken);

        var clientByTrip = tripClients.ToDictionary(t => t.Id, t => (t.ClientId, t.ClientName));
        var legsByShipment = legs.GroupBy(l => l.ShipmentId).ToDictionary(g => g.Key, g => g.ToList());

        return
        [
            .. rows.Select(row => ToResponse(
                row,
                legsByShipment.TryGetValue(row.Id, out var own) ? own : [],
                clientByTrip))
        ];
    }

    private static ShipmentResponse ToResponse(
        ShipmentReadModel row,
        List<ShipmentLegReadModel> legs,
        Dictionary<Guid, (Guid? ClientId, string? ClientName)> clientByTrip) => new(
        row.Id,
        row.ShipmentNumber,
        row.Status,
        row.Kind,
        row.ClientId,
        row.ClientName,
        row.PoNumber,
        row.ConsignorName,
        row.ConsignorContact,
        row.ConsigneeName,
        row.ConsigneeContact,
        row.OriginStopId,
        row.OriginName,
        row.DestinationStopId,
        row.DestinationName,
        row.Description,
        row.Pieces,
        row.WeightKg,
        row.LengthCm,
        row.WidthCm,
        row.HeightCm,
        row.Hazmat,
        row.DeclaredValueCad,
        row.SpecialHandling,
        row.Secured,
        row.ChargeCad,
        row.PaymentMethod,
        row.PaymentCollectedAtUtc,
        row.ReadyDate,
        row.RequiredByDate,
        [.. legs.Select(leg => ToLegResponse(leg, clientByTrip))],
        row.AwaitingTransfer,
        row.DeliveredAtUtc,
        row.ReceivedBy,
        row.DeliveryNote,
        row.CancelledReason,
        row.WrittenOffReason,
        row.Source,
        row.EnteredBy,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);

    private static ShipmentLegResponse ToLegResponse(
        ShipmentLegReadModel leg,
        Dictionary<Guid, (Guid? ClientId, string? ClientName)> clientByTrip)
    {
        var tripClient = clientByTrip.TryGetValue(leg.TripId, out var found) ? found : (null, null);

        return new ShipmentLegResponse(
            leg.Id,
            leg.Sequence,
            leg.TripId,
            leg.TripNumber,
            leg.TripServiceDate,
            tripClient.Item1,
            tripClient.Item2,
            leg.FromStopId,
            leg.FromName,
            leg.ToStopId,
            leg.ToName,
            leg.Status,
            leg.IsFinalLeg,
            leg.OnwardTripNumber,
            leg.AssignedAtUtc,
            leg.PickedUpAtUtc,
            leg.PickedUpBy,
            leg.DroppedAtUtc,
            leg.DroppedBy);
    }

    private static TripCargoResponse ToCargo(ShipmentLegReadModel leg) => new(
        leg.ShipmentId,
        leg.ShipmentNumber,
        leg.ShipmentStatus,
        leg.Kind,
        leg.Description,
        leg.ClientId,
        leg.ClientName,
        leg.ConsigneeName,
        leg.Pieces,
        leg.WeightKg,
        leg.ChargeCad,
        leg.Hazmat,
        leg.Secured,
        leg.Sequence,
        leg.Status,
        leg.IsFinalLeg,
        leg.OnwardTripNumber);
}
