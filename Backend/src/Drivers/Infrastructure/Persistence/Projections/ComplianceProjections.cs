using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Drivers.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="HosException"/> into <c>drivers.rm_hos_exceptions</c> (one row per aggregate, same id).</summary>
internal sealed class HosExceptionProjection : DriversProjection<HosException, HosExceptionReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(HosException));

    protected override void Map(HosException source, HosExceptionReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.DriverId = source.DriverId;
        row.Kind = HosDisplay.ExceptionKindToWire(source.Kind);
        row.Status = source.Status.ToString();
        row.Applying = source.IsApplying;
        row.TripId = source.TripId;
        row.DeclaredAtUtc = source.DeclaredAtUtc;
        row.DeclaredByUserId = source.DeclaredByUserId;
        row.EffectiveFromUtc = source.EffectiveFromUtc;
        row.EffectiveToUtc = source.EffectiveToUtc;
        row.Reason = source.Reason;
        row.Location = source.Location;
        row.NotKnownBeforeDeparture = source.NotKnownBeforeDeparture;
        row.ExtraDrivingMinutes = source.ExtraDriving is { } extra ? (int)Math.Round(extra.TotalMinutes) : null;
        row.DeferredOffDutyMinutes = source.DeferredOffDuty is { } deferred ? (int)Math.Round(deferred.TotalMinutes) : null;
        row.EldRemarkRecorded = source.EldRemarkRecorded;
        row.DispatchAcknowledgedAtUtc = source.DispatchAcknowledgedAtUtc;
        row.DispatchAcknowledgedByUserId = source.DispatchAcknowledgedByUserId;
        row.ApprovedAtUtc = source.ApprovedAtUtc;
        row.ApprovedByUserId = source.ApprovedByUserId;
        row.ClosedAtUtc = source.ClosedAtUtc;
        row.ClosedByUserId = source.ClosedByUserId;
        row.ClosureNote = source.ClosureNote;
        row.Version = source.Version;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
    }
}

/// <summary>Projects <see cref="DriverOutOfService"/> into <c>drivers.rm_driver_out_of_service</c>.</summary>
internal sealed class DriverOutOfServiceProjection : DriversProjection<DriverOutOfService, DriverOutOfServiceReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(DriverOutOfService));

    protected override void Map(DriverOutOfService source, DriverOutOfServiceReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.DriverId = source.DriverId;
        row.Status = source.Status.ToString();
        row.DeclaredAtUtc = source.DeclaredAtUtc;
        row.RecordedAtUtc = source.RecordedAtUtc;
        row.RecordedByUserId = source.RecordedByUserId;
        row.UntilUtc = source.UntilUtc;
        row.EffectiveUntilUtc = source.EffectiveUntilUtc;
        row.Reason = source.Reason;
        row.Evidence = source.Evidence;
        row.LiftedAtUtc = source.LiftedAtUtc;
        row.LiftedByUserId = source.LiftedByUserId;
        row.LiftReason = source.LiftReason;
        row.Version = source.Version;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
    }
}

/// <summary>Projects <see cref="EldMalfunctionReport"/> into <c>drivers.rm_eld_malfunction_reports</c>.</summary>
internal sealed class EldMalfunctionReportProjection : DriversProjection<EldMalfunctionReport, EldMalfunctionReportReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(EldMalfunctionReport));

    protected override void Map(EldMalfunctionReport source, EldMalfunctionReportReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.DriverId = source.DriverId;
        row.DriverName = source.DriverName;
        row.VehicleId = source.VehicleId;
        row.Code = source.Code;
        row.Status = source.Status.ToString();
        row.NoticedAtUtc = source.NoticedAtUtc;
        row.ReportedAtUtc = source.ReportedAtUtc;
        row.ReportedByUserId = source.ReportedByUserId;
        row.Description = source.Description;
        row.ResolvedAtUtc = source.ResolvedAtUtc;
        row.ResolvedByUserId = source.ResolvedByUserId;
        row.ResolutionNote = source.ResolutionNote;
        row.Version = source.Version;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
    }
}

/// <summary>Projects <see cref="ComplianceFinding"/> into <c>drivers.rm_compliance_findings</c>.</summary>
internal sealed class ComplianceFindingProjection : DriversProjection<ComplianceFinding, ComplianceFindingReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(ComplianceFinding));

    protected override void Map(ComplianceFinding source, ComplianceFindingReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.DriverId = source.DriverId;
        row.LocalDate = source.LocalDate;
        row.Rule = source.Rule;
        row.Severity = source.Severity.ToString();
        row.Status = source.Status.ToString();
        row.Source = source.Source.ToString();
        row.Description = source.Description;
        row.FollowUpNote = source.FollowUpNote;
        row.OpenedAtUtc = source.OpenedAtUtc;
        row.OpenedByUserId = source.OpenedByUserId;
        row.ClosedAtUtc = source.ClosedAtUtc;
        row.ClosedByUserId = source.ClosedByUserId;
        row.Version = source.Version;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
    }
}
