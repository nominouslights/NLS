using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Drivers.SelfAccess;

/// <summary>
/// The Drivers module's half of the caller-owns-this-row check — the guard every
/// <c>/api/drivers/{driverId}/…</c> route on the DriverAccess group runs before it does anything.
///
/// <para>
/// <b>Why a policy is not enough.</b> <c>AuthorizationPolicies.DriverAccess</c> admits the Driver
/// role, which means it admits <i>every</i> driver. Without this check, driver A can
/// <c>POST /api/drivers/{B}/hos</c> — filing a legally binding duty log in driver B's name — and
/// read B's credentials and client clearances. Route-level authorization answers "what kind of
/// account is this?"; only this answers "is this their row?".
/// </para>
/// </summary>
public interface IDriverSelfAccess
{
    /// <summary>
    /// True when the caller may read or write <paramref name="driverId"/>'s records: they hold a
    /// dispatch role, or that driver row is their own.
    /// </summary>
    Task<bool> MayActOnDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
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
            async (userId, token) => (await drivers.GetByUserIdAsync(userId, token))?.Id,
            cancellationToken);
}
