using NorthernLink.Trips.Application.Shipments;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read-side queries over <c>trips.rm_shipments</c> / <c>trips.rm_shipment_legs</c>.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IShipmentReadService
{
    Task<ShipmentPageResponse> GetShipmentsAsync(
        ShipmentListFilter filter,
        CancellationToken cancellationToken = default);

    Task<ShipmentResponse?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cargo riding one trip, resolved by trip <b>number</b> — the join key the trip manifest
    /// already uses. Safe because <c>(tenant_id, trip_number)</c> is unique on <c>trips.trips</c>
    /// and <c>Trip.Update</c> never rewrites it.
    /// </summary>
    Task<IReadOnlyList<TripCargoResponse>> GetCargoForTripNumberAsync(
        string tripNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cargo for several trips at once, keyed by trip number — the manifest list path, so a page
    /// of manifests costs one query rather than one per row.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<TripCargoResponse>>> GetCargoForTripNumbersAsync(
        IReadOnlyCollection<string> tripNumbers,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filters for the shipment list. Every one is optional; combining them narrows. Mirrors the
/// trips list filter, including its clamp-don't-reject paging.
/// </summary>
public sealed record ShipmentListFilter
{
    public const int MaxPageSize = 200;

    /// <summary>Exact status name, matched ordinally against the enum's names — never parsed as a number.</summary>
    public string? Status { get; init; }

    public string? Kind { get; init; }

    /// <summary>Pre-registered cargo waiting for a run: no legs at all.</summary>
    public bool UnassignedOnly { get; init; }

    public Guid? TripId { get; init; }
    public string? TripNumber { get; init; }

    /// <summary>The shipment's own client — the party who pays, never the trip's.</summary>
    public Guid? ClientId { get; init; }

    /// <summary>Counter sales, and the legacy-backfill backlog when combined with Delivered.</summary>
    public bool ClientlessOnly { get; init; }

    /// <summary>Assigned or in transit — everything the operation still owes someone.</summary>
    public bool AwaitingDelivery { get; init; }

    /// <summary>Dropped at a hub with an onward leg still planned.</summary>
    public bool AwaitingTransferOnly { get; init; }

    /// <summary>Delivered, with a client and a charge — Billing's queue.</summary>
    public bool BillableOnly { get; init; }

    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>Free text over shipment number, description, and consignee.</summary>
    public string? Query { get; init; }

    public int? Page { get; init; }
    public int? PageSize { get; init; }
}
