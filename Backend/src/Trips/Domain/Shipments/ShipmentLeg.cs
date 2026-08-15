using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>
/// One trip a shipment rides on its way to the consignee. A shipment with two legs is freight
/// that transfers at a hub — carried out on one run, dropped, then picked up by another — while
/// staying <em>one</em> billable item with one charge.
/// <para>
/// <b>This is the first child-entity table in the codebase; everything else owned is jsonb.</b>
/// That is deliberate and load-bearing: the two questions this whole feature exists to answer —
/// "what cargo is on TR-4824" and "everything for this client across whatever trips carried it"
/// — are both cross-row queries, which a jsonb array cannot serve. <c>Trip.Stops</c> is jsonb
/// precisely because nobody queries across it; this is the opposite case.
/// </para>
/// <para>
/// <see cref="TripNumber"/> and <see cref="TripServiceDate"/> are snapshots taken at assignment.
/// The trip number is safe to snapshot and to join on because <c>(tenant_id, trip_number)</c> is
/// unique on <c>trips.trips</c> and <c>Trip.Update</c> never writes it — that immutability is
/// what lets the trip manifest resolve its cargo by trip number alone, with no join to
/// <c>trips.trips</c> at all. <see cref="TripId"/> stays authoritative for everything else.
/// </para>
/// <para>
/// Mutating methods are <c>internal</c>: a leg only ever changes through its
/// <see cref="Shipment"/>, which owns the invariants about ordering and about what the
/// shipment's own status becomes.
/// </para>
/// </summary>
public sealed class ShipmentLeg : Entity, ITenantScoped
{
    private ShipmentLeg()
    {
        // EF Core materialization only.
        TripNumber = null!;
        FromName = null!;
        ToName = null!;
    }

    /// <summary>
    /// Copied from the owning shipment. Redundant in the object model — a leg is only ever
    /// reachable through its shipment — but non-negotiable in the database: a leg row is
    /// tenant-scoped data, and the platform rule is that every such table carries its own
    /// <c>tenant_id</c> with its own RLS policy. Inheriting isolation through a foreign key is
    /// exactly the "protected by association" pattern the RLS convention forbids.
    /// </summary>
    public Guid TenantId { get; private set; }

    public Guid ShipmentId { get; private set; }

    /// <summary>1-based position in the route. Legs run in this order; gaps are closed on removal.</summary>
    public int Sequence { get; private set; }

    public Guid TripId { get; private set; }
    public string TripNumber { get; private set; }
    public DateOnly TripServiceDate { get; private set; }

    public Guid? FromStopId { get; private set; }
    public string FromName { get; private set; }
    public Guid? ToStopId { get; private set; }
    public string ToName { get; private set; }

    public ShipmentLegStatus Status { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }
    public DateTimeOffset? PickedUpAtUtc { get; private set; }
    public string? PickedUpBy { get; private set; }
    public DateTimeOffset? DroppedAtUtc { get; private set; }
    public string? DroppedBy { get; private set; }

    public bool IsFinished => Status == ShipmentLegStatus.Dropped;

    internal static ShipmentLeg Create(
        Guid tenantId,
        Guid shipmentId,
        int sequence,
        Guid tripId,
        string tripNumber,
        DateOnly tripServiceDate,
        Guid? fromStopId,
        string? fromName,
        Guid? toStopId,
        string? toName) => new()
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            Sequence = sequence,
            TripId = tripId,
            TripNumber = tripNumber.Trim(),
            TripServiceDate = tripServiceDate,
            FromStopId = fromStopId,
            FromName = fromName?.Trim() ?? string.Empty,
            ToStopId = toStopId,
            ToName = toName?.Trim() ?? string.Empty,
            Status = ShipmentLegStatus.Planned,
            AssignedAtUtc = DateTimeOffset.UtcNow,
        };

    internal void Resequence(int sequence) => Sequence = sequence;

    internal void MarkPickedUp(DateTimeOffset atUtc, string? by)
    {
        Status = ShipmentLegStatus.PickedUp;
        PickedUpAtUtc = atUtc;
        PickedUpBy = string.IsNullOrWhiteSpace(by) ? null : by.Trim();
    }

    internal void MarkDropped(DateTimeOffset atUtc, string? by)
    {
        Status = ShipmentLegStatus.Dropped;
        DroppedAtUtc = atUtc;
        DroppedBy = string.IsNullOrWhiteSpace(by) ? null : by.Trim();
    }
}
