namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// How a passenger's fare was settled on a community run — the runs that collect from riders
/// rather than invoicing a client, and therefore never enter the billing arc at all.
/// <para>
/// Stored inside the passengers jsonb column, converted to its <em>name</em> rather than EF's
/// default integer: a jsonb payload is read by humans and by hand-written SQL far more often
/// than a relational column, and an opaque 0/1/2 there is a trap.
/// </para>
/// </summary>
public enum FarePaymentMethod
{
    /// <summary>Cash taken by the driver or dispatcher.</summary>
    Cash,

    /// <summary>Paid at booking time through the community booking flow.</summary>
    Online,

    /// <summary>No fare charged — a waived seat. Recorded so it is a decision, not a gap.</summary>
    Waived,
}
