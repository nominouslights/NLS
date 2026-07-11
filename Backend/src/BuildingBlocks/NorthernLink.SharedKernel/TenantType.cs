namespace NorthernLink.SharedKernel;

/// <summary>
/// The platform's three tenant categories plus the non-tenant consumer pool.
/// See the northern-link-architecture skill, Section 3 (Tenant Model).
/// </summary>
public enum TenantType
{
    /// <summary>Northern Link itself — owner, dispatchers, supervisors, accountant, drivers.</summary>
    Internal,

    /// <summary>A contracted client organization (e.g. Alamos Gold).</summary>
    Client,

    /// <summary>A vendor or partner operator (e.g. Miller the Mover, feeder-route operators).</summary>
    VendorPartner,

    /// <summary>Individual consumers — community passengers, NIHB clients, grocery/parcel customers.</summary>
    Consumer,
}
