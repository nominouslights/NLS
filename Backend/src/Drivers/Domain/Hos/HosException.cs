using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// A declared or approved exception to the hours-of-service limits — the record behind the only
/// overrides the safety policy allows past the hours gate. Three kinds (<see cref="HosExceptionKind"/>):
/// adverse driving conditions and an emergency are <b>declared</b> by the driver (or by dispatch on
/// their behalf) within 24 hours of the conditions arising and apply at once; an off-duty
/// deferral is <b>approved</b> by a compliance officer ahead of a specific trip and is born
/// <see cref="HosExceptionStatus.Approved"/>. Rejecting or withdrawing stops an exception
/// applying immediately — the rule engine only ever sees the ones whose status is Declared,
/// Acknowledged or Approved (<see cref="IsApplying"/>).
///
/// <para>
/// The rules here are the regulation's own shape of each exception: adverse conditions must
/// not have been knowable before departure (s.76) and extend the limits by at most two hours;
/// a deferral names its trip and starts in the future (the Trips module checks it against the
/// trip's own day — this library cannot see trips). Whether the exception actually rescues the
/// hours is the engine's call, not this aggregate's: it stores the declaration, the engine
/// applies it (or reports it declared outside a shift, duplicated, and so on as ledger problems).
/// </para>
/// </summary>
public sealed class HosException : AggregateRoot, ITenantScoped
{
    public const int ReasonMaxLength = 500;
    public const int LocationMaxLength = 200;
    public const int NoteMaxLength = 1000;

    /// <summary>s.76: an adverse-conditions or emergency declaration must reach the carrier within a day.</summary>
    public static readonly TimeSpan DeclarationWindow = TimeSpan.FromHours(24);

    /// <summary>How far ahead a deferral may be approved — a planning horizon, not a regulatory one.</summary>
    public static readonly TimeSpan DeferralPlanningWindow = TimeSpan.FromDays(7);

    public static readonly TimeSpan MinimumExtension = TimeSpan.FromMinutes(1);

    private static readonly HosCalendar DefaultCalendar = new();

    private HosException()
    {
        // EF Core materialization only.
        Reason = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid DriverId { get; private set; }
    public HosExceptionKind Kind { get; private set; }
    public HosExceptionStatus Status { get; private set; }

    /// <summary>The trip this exception belongs to, when known. Opaque — no reference into Trips.</summary>
    public Guid? TripId { get; private set; }

    public DateTimeOffset DeclaredAtUtc { get; private set; }
    public Guid DeclaredByUserId { get; private set; }

    /// <summary>When the conditions arose (the instant the engine applies the exception at).</summary>
    public DateTimeOffset EffectiveFromUtc { get; private set; }

    /// <summary>When they ended; null = still in force (an emergency ends with the driving segment in progress).</summary>
    public DateTimeOffset? EffectiveToUtc { get; private set; }

    public string Reason { get; private set; }
    public string? Location { get; private set; }

    /// <summary>s.76's precondition for adverse conditions: they could not reasonably have been known before departure.</summary>
    public bool NotKnownBeforeDeparture { get; private set; }

    /// <summary>Adverse conditions only: how much extra driving / on-duty / elapsed time is claimed (≤ 2h).</summary>
    public TimeSpan? ExtraDriving { get; private set; }

    /// <summary>Off-duty deferral only: how much of day one's off-duty time moves to day two (≤ 2h).</summary>
    public TimeSpan? DeferredOffDuty { get; private set; }

    /// <summary>Whether the driver has annotated the ELD (the RODS) with this exception.</summary>
    public bool EldRemarkRecorded { get; private set; }

    public DateTimeOffset? DispatchAcknowledgedAtUtc { get; private set; }
    public Guid? DispatchAcknowledgedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }

    /// <summary>When it stopped applying (rejected or withdrawn), by whom, and why.</summary>
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public string? ClosureNote { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>True while the rule engine should honour this exception.</summary>
    public bool IsApplying => Status is HosExceptionStatus.Declared or HosExceptionStatus.Acknowledged or HosExceptionStatus.Approved;

    /// <summary>
    /// Records a driver-declared exception (adverse conditions or emergency). The caller proves
    /// the actor may act on this driver's record; here the declaration must be fresh (inside the
    /// last 24 hours, not more than five minutes ahead), carry a reason, and — for adverse
    /// conditions — attest that they were not knowable before departure and claim an extension
    /// of one minute to two hours. A deferral cannot be declared this way.
    /// </summary>
    public static Result<HosException> Declare(
        Guid tenantId,
        Guid driverId,
        HosExceptionKind kind,
        Guid declaredByUserId,
        DateTimeOffset now,
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset? effectiveToUtc,
        string? reason,
        string? location,
        bool notKnownBeforeDeparture,
        TimeSpan? extraDriving,
        bool eldRemarkRecorded,
        Guid? tripId)
    {
        if (!Enum.IsDefined(kind) || kind == HosExceptionKind.OffDutyDeferral)
        {
            return Result.Failure<HosException>(kind == HosExceptionKind.OffDutyDeferral
                ? ComplianceErrors.DeferralRequiresComplianceOfficer
                : ComplianceErrors.ExceptionKindInvalid);
        }

        var reasonResult = CleanReason(reason);
        if (reasonResult.IsFailure)
        {
            return Result.Failure<HosException>(reasonResult.Error);
        }

        if (effectiveFromUtc > now.AddSeconds(HosLimits.FutureSkewSeconds))
        {
            return Result.Failure<HosException>(ComplianceErrors.ExceptionInFuture);
        }

        if (effectiveFromUtc < now - DeclarationWindow)
        {
            return Result.Failure<HosException>(ComplianceErrors.ExceptionBackdated);
        }

        if (effectiveToUtc is { } to && to < effectiveFromUtc)
        {
            return Result.Failure<HosException>(ComplianceErrors.ExceptionSpanInvalid);
        }

        TimeSpan? extension = null;
        if (kind == HosExceptionKind.AdverseDrivingConditions)
        {
            if (!notKnownBeforeDeparture)
            {
                return Result.Failure<HosException>(ComplianceErrors.AdverseKnowabilityRequired);
            }

            if (extraDriving is not { } extra || extra < MinimumExtension)
            {
                return Result.Failure<HosException>(ComplianceErrors.AdverseExtensionRequired);
            }

            if (extra.TotalSeconds > HosLimits.AdverseMaxExtensionSeconds)
            {
                return Result.Failure<HosException>(ComplianceErrors.AdverseExtensionTooLong);
            }

            extension = TimeSpan.FromSeconds(Math.Floor(extra.TotalSeconds));
        }

        var exception = new HosException
        {
            TenantId = tenantId,
            DriverId = driverId,
            Kind = kind,
            Status = HosExceptionStatus.Declared,
            TripId = tripId,
            DeclaredAtUtc = now,
            DeclaredByUserId = declaredByUserId,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveToUtc = effectiveToUtc,
            Reason = reasonResult.Value,
            Location = Clean(location, LocationMaxLength),
            NotKnownBeforeDeparture = notKnownBeforeDeparture,
            ExtraDriving = extension,
            EldRemarkRecorded = eldRemarkRecorded,
            UpdatedAtUtc = now,
        };

        exception.Raise(new HosExceptionDeclaredDomainEvent(exception.Id, driverId, tenantId, kind));
        return Result.Success(exception);
    }

    /// <summary>
    /// A compliance officer approves an off-duty deferral (s.16) for a specific trip. Born
    /// <see cref="HosExceptionStatus.Approved"/> with the officer as both declarer and approver.
    /// Day one is the America/Winnipeg day <paramref name="effectiveFromUtc"/> falls in; the
    /// exception runs to the end of day two. The caller proves the actor holds ComplianceAccess.
    /// </summary>
    public static Result<HosException> ApproveDeferral(
        Guid tenantId,
        Guid driverId,
        Guid approvedByUserId,
        DateTimeOffset now,
        Guid? tripId,
        DateTimeOffset effectiveFromUtc,
        TimeSpan? deferredOffDuty,
        string? reason,
        string? location,
        bool eldRemarkRecorded,
        HosCalendar? calendar = null)
    {
        if (tripId is not { } trip || trip == Guid.Empty)
        {
            return Result.Failure<HosException>(ComplianceErrors.DeferralTripRequired);
        }

        var reasonResult = CleanReason(reason);
        if (reasonResult.IsFailure)
        {
            return Result.Failure<HosException>(reasonResult.Error);
        }

        if (effectiveFromUtc <= now)
        {
            return Result.Failure<HosException>(ComplianceErrors.DeferralNotInFuture);
        }

        if (effectiveFromUtc >= now + DeferralPlanningWindow)
        {
            return Result.Failure<HosException>(ComplianceErrors.DeferralTooFarAhead);
        }

        if (deferredOffDuty is not { } deferred
            || deferred < MinimumExtension
            || deferred.TotalSeconds > HosLimits.DeferralMaxSeconds)
        {
            return Result.Failure<HosException>(ComplianceErrors.DeferralAmountInvalid);
        }

        var cal = calendar ?? DefaultCalendar;
        var dayOne = cal.DayOf(effectiveFromUtc.ToUnixTimeSeconds());
        var effectiveTo = DateTimeOffset.FromUnixTimeSeconds(cal.DayEndUnix(dayOne.AddDays(1)));

        var exception = new HosException
        {
            TenantId = tenantId,
            DriverId = driverId,
            Kind = HosExceptionKind.OffDutyDeferral,
            Status = HosExceptionStatus.Approved,
            TripId = trip,
            DeclaredAtUtc = now,
            DeclaredByUserId = approvedByUserId,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveToUtc = effectiveTo,
            Reason = reasonResult.Value,
            Location = Clean(location, LocationMaxLength),
            NotKnownBeforeDeparture = false,
            DeferredOffDuty = TimeSpan.FromSeconds(Math.Floor(deferred.TotalSeconds)),
            EldRemarkRecorded = eldRemarkRecorded,
            ApprovedAtUtc = now,
            ApprovedByUserId = approvedByUserId,
            UpdatedAtUtc = now,
        };

        exception.Raise(new HosExceptionDeclaredDomainEvent(exception.Id, driverId, tenantId, HosExceptionKind.OffDutyDeferral));
        return Result.Success(exception);
    }

    /// <summary>
    /// Dispatch (or compliance) marks the declaration seen. Declared → Acknowledged; an already
    /// acknowledged or approved exception is left alone (no event, no write). The caller proves
    /// the role.
    /// </summary>
    public Result Acknowledge(Guid byUserId, DateTimeOffset now)
    {
        switch (Status)
        {
            case HosExceptionStatus.Acknowledged:
            case HosExceptionStatus.Approved:
                return Result.Success();
            case HosExceptionStatus.Rejected:
            case HosExceptionStatus.Withdrawn:
                return Result.Failure(ComplianceErrors.ExceptionClosed);
        }

        Status = HosExceptionStatus.Acknowledged;
        DispatchAcknowledgedAtUtc = now;
        DispatchAcknowledgedByUserId = byUserId;
        UpdatedAtUtc = now;

        Raise(new HosExceptionStatusChangedDomainEvent(Id, DriverId, TenantId, Status));
        return Result.Success();
    }

    /// <summary>
    /// A compliance officer refuses the exception; it stops applying at once. Rejecting twice is
    /// a no-op; rejecting a withdrawn one is a conflict. The caller proves the role.
    /// </summary>
    public Result Reject(Guid byUserId, DateTimeOffset now, string? note)
    {
        if (Status == HosExceptionStatus.Rejected)
        {
            return Result.Success();
        }

        if (Status == HosExceptionStatus.Withdrawn)
        {
            return Result.Failure(ComplianceErrors.ExceptionClosed);
        }

        Close(HosExceptionStatus.Rejected, byUserId, now, note);
        return Result.Success();
    }

    /// <summary>
    /// Takes the exception back. The declarer (or dispatch) may withdraw only while it is still
    /// <see cref="HosExceptionStatus.Declared"/>; a compliance officer
    /// (<paramref name="complianceOfficer"/>) may withdraw at any applying status. Withdrawing
    /// twice is a no-op; withdrawing a rejected one is a conflict.
    /// </summary>
    public Result Withdraw(Guid byUserId, DateTimeOffset now, bool complianceOfficer, string? note)
    {
        if (Status == HosExceptionStatus.Withdrawn)
        {
            return Result.Success();
        }

        if (Status == HosExceptionStatus.Rejected)
        {
            return Result.Failure(ComplianceErrors.ExceptionClosed);
        }

        if (!complianceOfficer && Status != HosExceptionStatus.Declared)
        {
            return Result.Failure(ComplianceErrors.ExceptionWithdrawRequiresComplianceOfficer);
        }

        Close(HosExceptionStatus.Withdrawn, byUserId, now, note);
        return Result.Success();
    }

    /// <summary>True when the exception's span touches [<paramref name="fromUnix"/>, <paramref name="toUnix"/>).</summary>
    public bool Overlaps(long fromUnix, long toUnix)
    {
        var from = EffectiveFromUtc.ToUnixTimeSeconds();
        var to = EffectiveToUtc?.ToUnixTimeSeconds() ?? long.MaxValue;
        return from < toUnix && to >= fromUnix;
    }

    private void Close(HosExceptionStatus status, Guid byUserId, DateTimeOffset now, string? note)
    {
        Status = status;
        ClosedAtUtc = now;
        ClosedByUserId = byUserId;
        ClosureNote = Clean(note, NoteMaxLength);
        UpdatedAtUtc = now;

        Raise(new HosExceptionStatusChangedDomainEvent(Id, DriverId, TenantId, Status));
    }

    private static Result<string> CleanReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<string>(ComplianceErrors.ExceptionReasonRequired);
        }

        var trimmed = reason.Trim();
        return trimmed.Length > ReasonMaxLength
            ? Result.Failure<string>(ComplianceErrors.ExceptionReasonTooLong)
            : Result.Success(trimmed);
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
