namespace NorthernLink.Shared.Tenancy;

/// <summary>
/// The platform's caller-owns-this-row guard — the check that sits <i>underneath</i> an
/// authorization policy on any route whose path names a record the caller might not own.
///
/// <para>
/// <b>Why this exists.</b> A policy answers "may this kind of account call this route at all?".
/// It cannot answer "may this account touch <i>this</i> row?", and on a route like
/// <c>POST /api/drivers/{driverId}/hos</c> the second question is the one that matters: a policy
/// admitting the Driver role admits <i>every</i> driver, so driver A can post a duty log — legally
/// binding compliance data — against driver B. Route-level authorization alone is not a tenant
/// boundary and not an ownership boundary; this is the ownership half.
/// </para>
///
/// <para>
/// <b>The rule.</b> A caller holding one of the <c>overrideRoles</c> (dispatch staff, in practice
/// <see cref="Kernel.Roles.DispatchAccess"/>) may act on any record — dispatch exists precisely to
/// act on other people's rows. Everyone else may act only on the record their own user id resolves
/// to. No principal means no self-record, so no access.
/// </para>
///
/// <para>
/// <b>How to adopt it in another module.</b> Supply a lookup from the caller's user id to the id
/// of the record they own in that module, and call <see cref="AllowsAsync"/> from the endpoint
/// before dispatching the command. The lookup is the module-specific half and stays in the module
/// (Drivers resolves <c>drivers.drivers.user_id</c> through <c>IDriverSelfAccess</c>); the
/// decision itself is here so every module makes it the same way, including the two easy
/// mistakes — forgetting that a missing principal must deny, and running the lookup before the
/// dispatch-role short-circuit.
/// </para>
///
/// <para>
/// This is an authorization check, not tenant scoping. It composes with, and never replaces,
/// the tenant filter + Postgres RLS that already keep one tenant's rows out of another's.
/// </para>
/// </summary>
public static class OwnRecordAccess
{
    /// <summary>
    /// Whether the caller holds any of <paramref name="roles"/>, compared <b>ordinally</b> — the
    /// same comparison <c>RequireRole</c> performs, and the reason role strings are constants in
    /// <see cref="Kernel.Roles"/> rather than literals. Deliberately not
    /// <c>ClaimsPrincipal.IsInRole</c>, whose case-insensitive match would accept a "dispatcher"
    /// that never should have been storable in the first place.
    /// </summary>
    public static bool HoldsAnyRole(this ICurrentActor actor, IReadOnlyCollection<string> roles)
    {
        foreach (var held in actor.Roles)
        {
            foreach (var role in roles)
            {
                if (string.Equals(held, role, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Decides whether the caller may act on <paramref name="requestedRecordId"/>.
    /// <para>
    /// <paramref name="ownRecordIdForUser"/> maps the caller's user id to the id of the record
    /// they own in the calling module, or null when they own none. It is invoked only after the
    /// override-role short-circuit fails, so a dispatcher's request never pays for the lookup.
    /// </para>
    /// </summary>
    /// <returns>True when the caller may proceed; false means respond 403, not 404 — the row
    /// exists and the caller simply is not its owner.</returns>
    public static async Task<bool> AllowsAsync(
        this ICurrentActor actor,
        Guid requestedRecordId,
        IReadOnlyCollection<string> overrideRoles,
        Func<Guid, CancellationToken, Task<Guid?>> ownRecordIdForUser,
        CancellationToken cancellationToken = default)
    {
        if (actor.HoldsAnyRole(overrideRoles))
        {
            return true;
        }

        // No principal (background work, or an unauthenticated flow that slipped past a policy)
        // has no self-record, so it can never satisfy the ownership half. Denying here is what
        // keeps a missing actor from reading as "owns everything".
        if (actor.UserId is not { } userId)
        {
            return false;
        }

        var ownRecordId = await ownRecordIdForUser(userId, cancellationToken);
        return ownRecordId is { } ownId && ownId == requestedRecordId;
    }
}
