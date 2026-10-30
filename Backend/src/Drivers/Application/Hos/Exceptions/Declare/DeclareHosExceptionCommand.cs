using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Declare;

/// <summary>
/// The driver (or dispatch on their behalf — the endpoint's own-record check decides who) declares
/// adverse driving conditions or an emergency. The declarer recorded is the token's user id. A
/// deferral cannot come this way: see <c>ApproveOffDutyDeferralCommand</c>.
/// </summary>
public sealed record DeclareHosExceptionCommand(
    Guid TenantId,
    Guid DriverId,
    HosExceptionKind Kind,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    string? Reason,
    string? Location,
    Guid? TripId,
    bool NotKnownBeforeDeparture,
    TimeSpan? ExtraDriving,
    bool EldRemarkRecorded) : ICommand<HosExceptionResponse>;
