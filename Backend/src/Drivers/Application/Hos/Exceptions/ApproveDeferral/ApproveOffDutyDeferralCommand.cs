using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.ApproveDeferral;

/// <summary>
/// A compliance officer approves an off-duty deferral (s.16) for <paramref name="TripId"/>,
/// starting at <paramref name="EffectiveFromUtc"/> (in the future, within seven days) and
/// deferring <paramref name="DeferredOffDuty"/> (one minute to two hours) of day one's off-duty
/// time to day two. The handler refuses any actor outside <c>Roles.ComplianceAccess</c>
/// underneath the route policy. The Trips module checks the deferral against the trip's own day
/// when the gate lands; this library cannot see trips.
/// </summary>
public sealed record ApproveOffDutyDeferralCommand(
    Guid TenantId,
    Guid DriverId,
    Guid? TripId,
    DateTimeOffset EffectiveFromUtc,
    TimeSpan? DeferredOffDuty,
    string? Reason,
    string? Location,
    bool EldRemarkRecorded) : ICommand<HosExceptionResponse>;
