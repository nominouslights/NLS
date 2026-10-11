using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Hos.Compliance;

/// <summary>Maps the compliance aggregates to the module's public response contracts.</summary>
public static class ComplianceResponseMapper
{
    public static HosExceptionResponse ToResponse(HosException e) => new(
        e.Id,
        e.DriverId,
        HosDisplay.ExceptionKindToWire(e.Kind),
        e.Status.ToString(),
        e.IsApplying,
        e.TripId,
        e.DeclaredAtUtc,
        e.DeclaredByUserId,
        e.EffectiveFromUtc,
        e.EffectiveToUtc,
        e.Reason,
        e.Location,
        e.NotKnownBeforeDeparture,
        Minutes(e.ExtraDriving),
        Minutes(e.DeferredOffDuty),
        e.EldRemarkRecorded,
        e.DispatchAcknowledgedAtUtc,
        e.DispatchAcknowledgedByUserId,
        e.ApprovedAtUtc,
        e.ApprovedByUserId,
        e.ClosedAtUtc,
        e.ClosedByUserId,
        e.ClosureNote);

    public static OutOfServiceRecordResponse ToResponse(DriverOutOfService r, DateTimeOffset now) => new(
        r.Id,
        r.DriverId,
        r.Status.ToString(),
        r.IsActiveAt(now),
        r.DeclaredAtUtc,
        r.RecordedAtUtc,
        r.RecordedByUserId,
        r.UntilUtc,
        r.EffectiveUntilUtc,
        r.Reason,
        r.Evidence,
        r.LiftedAtUtc,
        r.LiftedByUserId,
        r.LiftReason);

    public static EldMalfunctionResponse ToResponse(EldMalfunctionReport r) => new(
        r.Id,
        r.DriverId,
        r.VehicleId,
        r.Code,
        r.Status.ToString(),
        r.NoticedAtUtc,
        r.ReportedAtUtc,
        r.ReportedByUserId,
        r.Description,
        r.ResolvedAtUtc,
        r.ResolvedByUserId,
        r.ResolutionNote);

    public static ComplianceFindingResponse ToResponse(ComplianceFinding f) => new(
        f.Id,
        f.DriverId,
        f.LocalDate,
        f.Rule,
        f.Severity.ToString(),
        f.Status.ToString(),
        f.Source.ToString(),
        f.Description,
        f.FollowUpNote,
        f.OpenedAtUtc,
        f.OpenedByUserId,
        f.ClosedAtUtc,
        f.ClosedByUserId);

    private static int? Minutes(TimeSpan? span) => span is { } s ? (int)Math.Round(s.TotalMinutes) : null;
}
