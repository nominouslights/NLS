namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>
/// How a shipment entered the system: recorded from the Driver Field App (a parcel handed over
/// at a stop) or entered by a dispatcher (pre-registration, or a transcribed paper waybill).
/// Stamped on every register and edit for source attribution in the audit log — the same
/// convention as <see cref="Manifests.ManifestSource"/>.
/// </summary>
public enum ShipmentSource
{
    App,
    Dispatcher,
}
