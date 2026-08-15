namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>
/// Everything descriptive about a shipment, as one parameter object shared by
/// <see cref="Shipment.Register"/> and <see cref="Shipment.UpdateDetails"/>.
/// <para>
/// A record rather than twenty positional parameters, and deliberately so:
/// <c>Trip.Schedule</c> already takes twenty-three, <c>Trip.Update</c> sixteen, and
/// <c>CreateTripCommandHandler</c> is mostly parameter shuffling as a result. Passing one
/// object also makes it structurally impossible for register and edit to drift apart, which
/// is the failure this shape is really guarding against.
/// </para>
/// <para>
/// <b>Dimensions are three independent nullables, not a value object.</b> The codebase's
/// value-object convention (see <c>Fleet.Domain.Vehicles.Vin</c>) is all-or-nothing behind a
/// <c>Result&lt;T&gt;</c> factory, and dispatchers routinely know the weight and the piece
/// count and nothing else — requiring L×W×H together would fail the common case.
/// </para>
/// </summary>
public sealed record ShipmentDetails
{
    public ShipmentKind Kind { get; init; } = ShipmentKind.Parcel;

    /// <summary>What the goods are. The only hard requirement on a shipment.</summary>
    public required string Description { get; init; }

    public int Pieces { get; init; } = 1;
    public decimal? WeightKg { get; init; }
    public decimal? LengthCm { get; init; }
    public decimal? WidthCm { get; init; }
    public decimal? HeightCm { get; init; }
    public bool Hazmat { get; init; }
    public decimal? DeclaredValueCad { get; init; }
    public string? SpecialHandling { get; init; }

    // Parties.
    public string? ConsignorName { get; init; }
    public string? ConsignorContact { get; init; }
    public string? ConsigneeName { get; init; }
    public string? ConsigneeContact { get; init; }

    // End-to-end corridor. Optional snapshot references to the Stop catalog, exactly like
    // RouteStop — free-text origins and destinations stay legal, so the manifest backfill and
    // a hand-written waybill both survive.
    public Guid? OriginStopId { get; init; }
    public string? OriginName { get; init; }
    public Guid? DestinationStopId { get; init; }
    public string? DestinationName { get; init; }

    // Pre-registration window. Both optional: cargo is frequently dropped off with no date
    // attached beyond "next run that way".
    public DateOnly? ReadyDate { get; init; }
    public DateOnly? RequiredByDate { get; init; }

    // --- Billing. The shipment's OWN client, never the trip's. See Shipment's class comment. ---
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
    public string? PoNumber { get; init; }
    public decimal? ChargeCad { get; init; }

    /// <summary>Counter payment — clientless shipments only. A client shipment settles by invoice.</summary>
    public ShipmentPaymentMethod? PaymentMethod { get; init; }
}
