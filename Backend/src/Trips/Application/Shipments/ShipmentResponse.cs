namespace NorthernLink.Trips.Application.Shipments;

/// <summary>
/// Public contract for a shipment (list and detail — same shape). Enum-typed fields travel as
/// their string names.
/// <para>
/// <b><see cref="ClientId"/>/<see cref="ClientName"/> are the shipment's own party, and
/// <see cref="Legs"/> carry the trip's.</b> They routinely differ — a run for Alamos full of
/// Incline Group's freight — so both are returned and the UI shows the mismatch rather than
/// picking one. Nothing here derives either from the other.
/// </para>
/// </summary>
public sealed record ShipmentResponse(
    Guid Id,
    string ShipmentNumber,
    string Status,
    string Kind,
    Guid? ClientId,
    string? ClientName,
    string? PoNumber,
    string? ConsignorName,
    string? ConsignorContact,
    string? ConsigneeName,
    string? ConsigneeContact,
    Guid? OriginStopId,
    string OriginName,
    Guid? DestinationStopId,
    string DestinationName,
    string Description,
    int Pieces,
    decimal? WeightKg,
    decimal? LengthCm,
    decimal? WidthCm,
    decimal? HeightCm,
    bool Hazmat,
    decimal? DeclaredValueCad,
    string? SpecialHandling,
    bool Secured,
    decimal? ChargeCad,
    string? PaymentMethod,
    DateTimeOffset? PaymentCollectedAtUtc,
    DateOnly? ReadyDate,
    DateOnly? RequiredByDate,
    IReadOnlyList<ShipmentLegResponse> Legs,

    /// <summary>Freight dropped at a hub with an onward leg still planned — real, locatable inventory.</summary>
    bool AwaitingTransfer,

    DateTimeOffset? DeliveredAtUtc,
    string? ReceivedBy,
    string? DeliveryNote,
    string? CancelledReason,
    string? WrittenOffReason,
    string Source,
    string? EnteredBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// One trip the shipment rides. <see cref="TripClientName"/> is the <em>trip's</em> client, joined
/// at read time rather than denormalised because it can change after the shipment is projected —
/// it exists so a screen can say "bills to Incline Group · carried on TR-4824 (Alamos)" without a
/// second request.
/// </summary>
public sealed record ShipmentLegResponse(
    Guid Id,
    int Sequence,
    Guid TripId,
    string TripNumber,
    DateOnly TripServiceDate,
    Guid? TripClientId,
    string? TripClientName,
    Guid? FromStopId,
    string FromName,
    Guid? ToStopId,
    string ToName,
    string Status,
    bool IsFinalLeg,
    string? OnwardTripNumber,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? PickedUpAtUtc,
    string? PickedUpBy,
    DateTimeOffset? DroppedAtUtc,
    string? DroppedBy);

/// <summary>A page of shipments, mirroring the trips list contract.</summary>
public sealed record ShipmentPageResponse(
    IReadOnlyList<ShipmentResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>
/// What a trip's cargo section shows, and what replaces the manifest's old §6 jsonb rows. A
/// read-only view: cargo is edited as shipments, never through the manifest.
/// </summary>
public sealed record TripCargoResponse(
    Guid ShipmentId,
    string ShipmentNumber,
    string Status,
    string Kind,
    string Description,
    Guid? ClientId,
    string? ClientName,
    string? ConsigneeName,
    int Pieces,
    decimal? WeightKg,
    decimal? ChargeCad,
    bool Hazmat,
    bool Secured,
    int Sequence,
    string LegStatus,
    bool IsFinalLeg,
    string? OnwardTripNumber);
