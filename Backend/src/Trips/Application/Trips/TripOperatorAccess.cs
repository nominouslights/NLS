using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Trips;

/// <summary>
/// The Trips half of the caller-owns-this-row check — what makes the DriverAccess policy
/// safe on the driver-facing <c>/api/trips</c> routes (list, detail, activity, status).
///
/// <para>
/// <b>Why a policy is not enough.</b> <c>AuthorizationPolicies.DriverAccess</c> admits the
/// Driver role, which means it admits <i>every</i> driver. Those routes carry no
/// <c>{driverId}</c>, so without this check driver A could read any trip and start or cancel
/// driver B's run. Route-level authorization answers "what kind of account is this?"; only
/// this answers "is this their trip?".
/// </para>
///
/// <para>
/// The caller is resolved to a driver through the replicated <see cref="Integration.DriverLookup.UserId"/>
/// (fed by <c>drivers.driver-changed</c>, including link/unlink), and the decision itself is
/// deferred to <see cref="OwnRecordAccess"/>, the platform-wide shape of this check: dispatch
/// roles act on any trip, everyone else only on the trip whose assigned driver is their own
/// row. An unassigned trip is nobody's, and an account linked to no driver owns nothing.
/// </para>
/// </summary>
public sealed class TripOperatorAccess(ICurrentActor actor, IDriverLookupRepository drivers)
{
    /// <summary>
    /// True when the caller may read or act on a trip assigned to
    /// <paramref name="assignedDriverId"/>: they hold a dispatch role, or that driver row is
    /// their own. Null (an unassigned trip) admits dispatch only.
    /// </summary>
    public Task<bool> MayOperateAsync(Guid? assignedDriverId, CancellationToken cancellationToken = default) =>
        assignedDriverId is { } driverId
            ? actor.AllowsAsync(driverId, Roles.DispatchAccess, OwnDriverIdAsync, cancellationToken)
            : Task.FromResult(actor.HoldsAnyRole(Roles.DispatchAccess));

    /// <summary>
    /// The scope a list read runs under: every trip for dispatch, the caller's own driver's
    /// trips for a linked driver account, and nothing for an account linked to no driver (or
    /// no principal at all — background work never reads as "sees everything").
    /// </summary>
    public async Task<TripOperatorScope> ResolveScopeAsync(CancellationToken cancellationToken = default)
    {
        if (actor.HoldsAnyRole(Roles.DispatchAccess))
        {
            return TripOperatorScope.Unrestricted;
        }

        if (actor.UserId is not { } userId)
        {
            return TripOperatorScope.Nothing;
        }

        var ownDriverId = await OwnDriverIdAsync(userId, cancellationToken);
        return ownDriverId is { } driverId ? TripOperatorScope.OwnTrips(driverId) : TripOperatorScope.Nothing;
    }

    private async Task<Guid?> OwnDriverIdAsync(Guid userId, CancellationToken cancellationToken) =>
        (await drivers.GetByUserIdAsync(userId, cancellationToken))?.DriverId;
}

/// <summary>
/// What a caller may see of the trip list. <see cref="IsUnrestricted"/> for dispatch;
/// otherwise <see cref="DriverId"/> is the only driver whose trips they may see, and null
/// means none at all.
/// </summary>
public sealed record TripOperatorScope(bool IsUnrestricted, Guid? DriverId)
{
    public static readonly TripOperatorScope Unrestricted = new(true, null);

    public static readonly TripOperatorScope Nothing = new(false, null);

    public static TripOperatorScope OwnTrips(Guid driverId) => new(false, driverId);
}
