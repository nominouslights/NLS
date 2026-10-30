using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// An out-of-service declaration (s.91) — enforcement told this driver to stop, and the platform
/// records it so nothing can assign, claim or start a trip for them while it stands. Recorded by
/// a compliance officer only (the handler proves the role), with the instant enforcement
/// declared it and the instant it runs to: 72 hours by default (the regulation's usual period),
/// an explicit end, or open-ended until lifted. At most one is
/// <see cref="OutOfServiceStatus.Active"/> per driver at a time (the handler checks); lifting it
/// before it would have expired requires a reason. Recording one also opens a Severe
/// <see cref="ComplianceFinding"/> — that happens in the handler, in the same transaction.
///
/// <para>
/// The rule engine receives the span [<see cref="DeclaredAtUtc"/>, <see cref="EffectiveUntilUtc"/>)
/// and blocks driving inside it (<c>OutOfService</c>), reporting any driving that happened
/// anyway as <c>DroveWhileOutOfService</c>.
/// </para>
/// </summary>
public sealed class DriverOutOfService : AggregateRoot, ITenantScoped
{
    public const int ReasonMaxLength = 500;
    public const int EvidenceMaxLength = 2000;

    /// <summary>The usual out-of-service period when enforcement gives no end time.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromHours(72);

    private DriverOutOfService()
    {
        // EF Core materialization only.
        Reason = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid DriverId { get; private set; }

    /// <summary>When enforcement declared it (the span's start).</summary>
    public DateTimeOffset DeclaredAtUtc { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }
    public Guid RecordedByUserId { get; private set; }

    /// <summary>When it runs to; null = until lifted.</summary>
    public DateTimeOffset? UntilUtc { get; private set; }

    public string Reason { get; private set; }

    /// <summary>Free text: inspection report number, officer, what was shown.</summary>
    public string? Evidence { get; private set; }

    public OutOfServiceStatus Status { get; private set; }
    public DateTimeOffset? LiftedAtUtc { get; private set; }
    public Guid? LiftedByUserId { get; private set; }
    public string? LiftReason { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// The instant the span ends for the rule engine: the lift, if it was lifted, otherwise
    /// <see cref="UntilUtc"/> (null = open).
    /// </summary>
    public DateTimeOffset? EffectiveUntilUtc => Status == OutOfServiceStatus.Lifted ? LiftedAtUtc : UntilUtc;

    /// <summary>True when the declaration is in force at <paramref name="at"/>.</summary>
    public bool IsActiveAt(DateTimeOffset at) =>
        at >= DeclaredAtUtc && (EffectiveUntilUtc is not { } until || at < until);

    /// <summary>True when the span touches [<paramref name="fromUnix"/>, <paramref name="toUnix"/>).</summary>
    public bool Overlaps(long fromUnix, long toUnix)
    {
        var from = DeclaredAtUtc.ToUnixTimeSeconds();
        var to = EffectiveUntilUtc?.ToUnixTimeSeconds() ?? long.MaxValue;
        return from < toUnix && to > fromUnix;
    }

    /// <summary>
    /// Records the declaration. <paramref name="untilUtc"/> null with <paramref name="openEnded"/>
    /// false means the 72-hour default; <paramref name="openEnded"/> true means until lifted.
    /// </summary>
    public static Result<DriverOutOfService> Record(
        Guid tenantId,
        Guid driverId,
        DateTimeOffset declaredAtUtc,
        Guid recordedByUserId,
        DateTimeOffset now,
        DateTimeOffset? untilUtc,
        bool openEnded,
        string? reason,
        string? evidence)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<DriverOutOfService>(ComplianceErrors.OutOfServiceReasonRequired);
        }

        if (declaredAtUtc > now.AddSeconds(HosLimits.FutureSkewSeconds))
        {
            return Result.Failure<DriverOutOfService>(ComplianceErrors.OutOfServiceDeclaredInFuture);
        }

        DateTimeOffset? until = openEnded ? null : untilUtc ?? declaredAtUtc + DefaultDuration;
        if (until is { } end && end <= declaredAtUtc)
        {
            return Result.Failure<DriverOutOfService>(ComplianceErrors.OutOfServiceUntilInvalid);
        }

        var record = new DriverOutOfService
        {
            TenantId = tenantId,
            DriverId = driverId,
            DeclaredAtUtc = declaredAtUtc,
            RecordedAtUtc = now,
            RecordedByUserId = recordedByUserId,
            UntilUtc = until,
            Reason = Truncate(reason.Trim(), ReasonMaxLength),
            Evidence = string.IsNullOrWhiteSpace(evidence) ? null : Truncate(evidence.Trim(), EvidenceMaxLength),
            Status = OutOfServiceStatus.Active,
            UpdatedAtUtc = now,
        };

        record.Raise(new DriverOutOfServiceRecordedDomainEvent(record.Id, driverId, tenantId, declaredAtUtc, until));
        return Result.Success(record);
    }

    /// <summary>
    /// Lifts the declaration. Early — before <see cref="UntilUtc"/>, or at any time when
    /// open-ended — requires a reason; lifting one that has already run its course does not.
    /// Lifting twice is a no-op.
    /// </summary>
    public Result Lift(Guid byUserId, DateTimeOffset now, string? reason)
    {
        if (Status == OutOfServiceStatus.Lifted)
        {
            return Result.Success();
        }

        var early = UntilUtc is not { } until || now < until;
        if (early && string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(ComplianceErrors.LiftReasonRequired);
        }

        Status = OutOfServiceStatus.Lifted;
        LiftedAtUtc = now;
        LiftedByUserId = byUserId;
        LiftReason = string.IsNullOrWhiteSpace(reason) ? null : Truncate(reason.Trim(), ReasonMaxLength);
        UpdatedAtUtc = now;

        Raise(new DriverOutOfServiceLiftedDomainEvent(Id, DriverId, TenantId, now));
        return Result.Success();
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
