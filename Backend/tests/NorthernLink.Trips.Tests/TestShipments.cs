using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Tests;

/// <summary>Factory helpers for the Shipment aggregate with valid baseline payloads.</summary>
internal static class TestShipments
{
    public static readonly Guid TenantId = TestPlanning.TenantId;

    /// <summary>Alamos Gold — the client the *trips* in these tests belong to.</summary>
    public static readonly Guid AlamosId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    /// <summary>Incline Group — a different company whose freight rides Alamos's runs.</summary>
    public static readonly Guid InclineId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    public static ShipmentDetails Details(
        string description = "Pallet of dry goods",
        ShipmentKind kind = ShipmentKind.Parcel,
        int pieces = 1,
        decimal? weightKg = 18m,
        Guid? clientId = null,
        string? clientName = null,
        string? poNumber = null,
        decimal? chargeCad = null,
        ShipmentPaymentMethod? paymentMethod = null,
        string origin = "Thompson",
        string destination = "Lynn Lake",
        string? consigneeName = "Lynn Lake community agent",
        DateOnly? readyDate = null,
        DateOnly? requiredByDate = null,
        decimal? declaredValueCad = null,
        bool hazmat = false) => new()
        {
            Description = description,
            Kind = kind,
            Pieces = pieces,
            WeightKg = weightKg,
            Hazmat = hazmat,
            DeclaredValueCad = declaredValueCad,
            ConsigneeName = consigneeName,
            OriginName = origin,
            DestinationName = destination,
            ReadyDate = readyDate,
            RequiredByDate = requiredByDate,
            ClientId = clientId,
            ClientName = clientName ?? (clientId is null ? null : "Incline Group"),
            PoNumber = poNumber,
            ChargeCad = chargeCad,
            PaymentMethod = paymentMethod,
        };

    /// <summary>A registered shipment; override only what the test cares about.</summary>
    public static Shipment Register(
        ShipmentDetails? details = null,
        string shipmentNumber = "SH-1001",
        ShipmentSource source = ShipmentSource.Dispatcher,
        string? enteredBy = "Dispatch",
        Guid? tenantId = null) =>
        Shipment.Register(
            tenantId ?? TenantId,
            shipmentNumber,
            details ?? Details(),
            source,
            enteredBy).Value;

    /// <summary>A billable shipment — Incline Group's freight, $250, ready to invoice on delivery.</summary>
    public static Shipment RegisterBillable(string shipmentNumber = "SH-1001") =>
        Register(Details(clientId: InclineId, clientName: "Incline Group", chargeCad: 250m), shipmentNumber);

    /// <summary>Routes a shipment through a trip and clears the events the test did not ask about.</summary>
    public static Shipment OnTrip(
        this Shipment shipment,
        Guid tripId,
        string tripNumber = "TR-1001",
        DateOnly? serviceDate = null,
        string from = "Thompson",
        string to = "Lynn Lake")
    {
        shipment.AddLeg(
            tripId,
            tripNumber,
            serviceDate ?? new DateOnly(2026, 7, 21),
            fromStopId: null,
            fromName: from,
            toStopId: null,
            toName: to);
        return shipment;
    }
}
