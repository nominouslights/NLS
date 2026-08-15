namespace NorthernLink.Shared.Tenancy;

/// <summary>
/// The authenticated person behind the current request — the "who" half of the ambient facts a
/// domain library needs but cannot resolve for itself, sitting beside <see cref="ITenantContext"/>
/// for exactly the same reason: the claims live in the gateway, and a domain library may
/// reference <c>NorthernLink.Shared</c> and nothing else.
/// <para>
/// <b>This is deliberately not the codebase's usual actor pattern.</b> Every other "who" on the
/// platform — <c>Shipment.EnteredBy</c>, <c>WorkOrder.CreatedBy</c>, <c>HosLogEntry.EnteredBy</c>
/// — is a string bound straight from the request body, so a caller can put any name it likes in
/// it. On an operational record that is a provenance note. On the chart of accounts that governs
/// every dollar in the business, a forgeable "who created this" field is worse than no field at
/// all, because it reads as authoritative in an audit and isn't. This value comes from a signed
/// token, server-side, and the caller cannot influence it.
/// </para>
/// <para>
/// Both properties are null for background work (the projection and outbox workers have no
/// principal) and for unauthenticated flows such as login. Callers must treat "no actor" as
/// normal rather than an error.
/// </para>
/// </summary>
public interface ICurrentActor
{
    /// <summary>The user id from the access token's <c>sub</c> claim.</summary>
    Guid? UserId { get; }

    /// <summary>
    /// The email from the access token's <c>email</c> claim. Convenience for logging and
    /// diagnostics — persisted records should store <see cref="UserId"/> and resolve the display
    /// value through a replica, so a future rename does not leave stale copies behind.
    /// </summary>
    string? Email { get; }
}
