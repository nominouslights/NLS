using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Drivers.SelfAccess;

/// <summary>
/// The Drivers module's half of the caller-owns-this-row check — the guard every
/// <c>/api/drivers/{driverId}/…</c> route on the DriverAccess and HosAccess groups runs before
/// it does anything.
///
/// <para>
/// <b>Why a policy is not enough.</b> <c>AuthorizationPolicies.DriverAccess</c> admits the Driver
/// role, which means it admits <i>every</i> driver. Without this check, driver A can
/// <c>POST /api/drivers/{B}/hos</c> — filing a legally binding duty log in driver B's name — and
/// read B's credentials and client clearances. Route-level authorization answers "what kind of
/// account is this?"; only this answers "is this their row?".
/// </para>
///
/// <para>
/// Three flavours, because three groups have three override sets: the Driver Field App's
/// surface (<see cref="MayActOnDriverAsync"/>, dispatch overrides), the HOS surface
/// (<see cref="MayActOnDriverHosAsync"/>, dispatch and compliance override — they transcribe
/// and amend other people's records for a living), and certification
/// (<see cref="IsThisDriverAsync"/>, nobody overrides — a record of duty status is certified by
/// the driver whose record it is, full stop).
/// </para>
/// </summary>
public interface IDriverSelfAccess
{
    /// <summary>
    /// True when the caller may read or write <paramref name="driverId"/>'s records: they hold a
    /// dispatch role, or that driver row is their own.
    /// </summary>
    Task<bool> MayActOnDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the caller may read or write <paramref name="driverId"/>'s hours of service:
    /// they hold one of <see cref="Roles.HosRecordOverride"/>, or that driver row is their own.
    /// </summary>
    Task<bool> MayActOnDriverHosAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>True only when the caller's own user is linked to <paramref name="driverId"/>. No override.</summary>
    Task<bool> IsThisDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the caller through <c>drivers.drivers.user_id</c> and defers the decision itself to
/// <see cref="OwnRecordAccess"/>, the platform-wide shape of this check.
/// <para>
/// The lookup goes to the <b>write</b> repository, not <c>IDriverReadService</c>: rm_drivers lags
/// by a projection poll, and a newly linked driver being denied access to their own duty log for
/// a second would read as a broken app, not as eventual consistency.
/// </para>
/// </summary>
public sealed class DriverSelfAccess(ICurrentActor actor, IDriverRepository drivers) : IDriverSelfAccess
{
    public Task<bool> MayActOnDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        actor.AllowsAsync(
            driverId,
            // Dispatch staff act on other people's rows for a living — that is the job.
            Roles.DispatchAccess,
            OwnDriverId,
            cancellationToken);

    public Task<bool> MayActOnDriverHosAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        actor.AllowsAsync(driverId, Roles.HosRecordOverride, OwnDriverId, cancellationToken);

    public Task<bool> IsThisDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        actor.AllowsAsync(driverId, overrideRoles: [], OwnDriverId, cancellationToken);

    private async Task<Guid?> OwnDriverId(Guid userId, CancellationToken token) =>
        (await drivers.GetByUserIdAsync(userId, token))?.Id;
}
