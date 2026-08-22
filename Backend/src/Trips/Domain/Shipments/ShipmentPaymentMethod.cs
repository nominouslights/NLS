namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>
/// How a <em>clientless</em> shipment was paid for at the counter. The direct analogue of
/// <see cref="Manifests.FarePaymentMethod"/> on a passenger: a walk-up parcel is settled at
/// handover and never reaches an invoice, so recording the method here is what stops it
/// reading as an unpaid gap.
/// <para>
/// Nothing downstream reconciles this against a bank deposit or a QuickBooks receipt — it
/// answers "what was collected", not "what was banked".
/// </para>
/// </summary>
public enum ShipmentPaymentMethod
{
    Cash,
    Online,

    /// <summary>Deliberately not charged — recorded so a free carry is a decision, not a gap.</summary>
    Waived,
}
