namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>
/// Where one leg of a shipment's route has got to. <see cref="Dropped"/> means the freight came
/// off that leg's trip — at a hub with another leg still to run, or at its final destination.
/// The handover to the consignee is a separate, shipment-level act
/// (<c>Shipment.RecordDelivery</c>), because a driver unloading at a transfer point and a
/// consignee signing for goods are not the same event.
/// </summary>
public enum ShipmentLegStatus
{
    Planned = 0,
    PickedUp = 1,
    Dropped = 2,
}
