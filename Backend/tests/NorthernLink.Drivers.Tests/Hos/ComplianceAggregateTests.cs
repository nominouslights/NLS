using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using Xunit;
using static NorthernLink.Drivers.Tests.Hos.HosTestSupport;

namespace NorthernLink.Drivers.Tests.Hos;

/// <summary>
/// The out-of-service declaration (72-hour default, early lift needs a reason), the ELD
/// malfunction report (open until compliance resolves it), the compliance finding (the rule
/// vocabulary and the one-way workflow), and the driver's prior-records attestation.
/// </summary>
public class ComplianceAggregateTests
{
    private static readonly Guid OfficerId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // ---- DriverOutOfService ----

    private static Result<DriverOutOfService> Oos(
        string declared = "14:00",
        DateTimeOffset? until = null,
        bool openEnded = false,
        string? reason = "Roadside inspection: logbook not produced",
        DateTimeOffset? declaredInstant = null) =>
        DriverOutOfService.Record(
            TestDrivers.TenantId, DriverId, declaredInstant ?? At(declared), OfficerId, Now, until, openEnded, reason, "MPI report 44-1021");

    [Fact]
    public void An_out_of_service_declaration_runs_72_hours_by_default_and_is_active_inside_that()
    {
        var result = Oos();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        var r = result.Value;
        Assert.Equal(OutOfServiceStatus.Active, r.Status);
        Assert.Equal(At("14:00").AddHours(72), r.UntilUtc);
        Assert.Equal(r.UntilUtc, r.EffectiveUntilUtc);
        Assert.Equal(OfficerId, r.RecordedByUserId);
        Assert.Equal("MPI report 44-1021", r.Evidence);
        Assert.True(r.IsActiveAt(Now));
        Assert.True(r.IsActiveAt(At("14:00").AddHours(72).AddSeconds(-1)));
        Assert.False(r.IsActiveAt(At("14:00").AddHours(72)));
        Assert.False(r.IsActiveAt(At("13:59")));
        var raised = Assert.IsType<DriverOutOfServiceRecordedDomainEvent>(Assert.Single(r.DomainEvents));
        Assert.Equal(r.UntilUtc, raised.UntilUtc);
    }

    [Fact]
    public void An_open_ended_declaration_has_no_end_until_lifted()
    {
        var r = Oos(openEnded: true).Value;

        Assert.Null(r.UntilUtc);
        Assert.True(r.IsActiveAt(Now.AddYears(1)));
        Assert.True(r.Overlaps(At("00:00", 30).ToUnixTimeSeconds(), At("00:00", 31).ToUnixTimeSeconds()));
    }

    [Fact]
    public void The_declaration_needs_a_reason_a_past_instant_and_an_end_after_its_start()
    {
        Assert.Equal(ComplianceErrors.OutOfServiceReasonRequired, Oos(reason: " ").Error);
        Assert.Equal(ComplianceErrors.OutOfServiceDeclaredInFuture, Oos(declaredInstant: Now.AddMinutes(6)).Error);
        Assert.Equal(ComplianceErrors.OutOfServiceUntilInvalid, Oos(until: At("14:00")).Error);
        Assert.True(Oos(until: At("14:01")).IsSuccess);
    }

    [Fact]
    public void Lifting_early_requires_a_reason_and_ends_the_span_at_the_lift()
    {
        var r = Oos().Value;
        r.ClearDomainEvents();

        Assert.Equal(ComplianceErrors.LiftReasonRequired, r.Lift(OfficerId, Now, "  ").Error);
        Assert.Equal(OutOfServiceStatus.Active, r.Status);

        Assert.True(r.Lift(OfficerId, Now, "Logbook produced at the detachment").IsSuccess);
        Assert.Equal(OutOfServiceStatus.Lifted, r.Status);
        Assert.Equal(Now, r.LiftedAtUtc);
        Assert.Equal(Now, r.EffectiveUntilUtc);
        Assert.Equal(OfficerId, r.LiftedByUserId);
        Assert.False(r.IsActiveAt(Now));
        Assert.IsType<DriverOutOfServiceLiftedDomainEvent>(Assert.Single(r.DomainEvents));

        r.ClearDomainEvents();
        Assert.True(r.Lift(OfficerId, Now.AddHours(1), null).IsSuccess);
        Assert.Empty(r.DomainEvents);
        Assert.Equal(Now, r.LiftedAtUtc);
    }

    [Fact]
    public void Lifting_after_the_period_ran_out_needs_no_reason()
    {
        var r = Oos().Value;

        Assert.True(r.Lift(OfficerId, At("14:00").AddHours(72).AddMinutes(1), null).IsSuccess);
        Assert.Equal(OutOfServiceStatus.Lifted, r.Status);
        Assert.Null(r.LiftReason);
    }

    // ---- EldMalfunctionReport ----

    private static Result<EldMalfunctionReport> Eld(string? code = "P", string? description = "Positioning compliance malfunction", DateTimeOffset? noticed = null) =>
        EldMalfunctionReport.Report(TestDrivers.TenantId, DriverId, "J. Spence", null, code, noticed ?? At("08:00"), UserId, Now, description);

    [Fact]
    public void A_malfunction_report_opens_with_the_code_and_the_drivers_name()
    {
        var result = Eld();

        Assert.True(result.IsSuccess);
        var r = result.Value;
        Assert.True(r.IsOpen);
        Assert.Equal("P", r.Code);
        Assert.Equal("J. Spence", r.DriverName);
        Assert.Equal(UserId, r.ReportedByUserId);
        Assert.True(r.Overlaps(At("00:00", 2).ToUnixTimeSeconds(), At("00:00", 3).ToUnixTimeSeconds()));
        Assert.False(r.Overlaps(At("00:00", -1).ToUnixTimeSeconds(), At("00:00").ToUnixTimeSeconds()));
        Assert.Equal("P", Assert.IsType<EldMalfunctionReportedDomainEvent>(Assert.Single(r.DomainEvents)).Code);
    }

    [Fact]
    public void A_malfunction_report_needs_a_code_a_description_and_a_past_instant()
    {
        Assert.Equal(ComplianceErrors.EldCodeRequired, Eld(code: "").Error);
        Assert.Equal(ComplianceErrors.EldCodeTooLong, Eld(code: new string('E', EldMalfunctionReport.CodeMaxLength + 1)).Error);
        Assert.Equal(ComplianceErrors.EldDescriptionRequired, Eld(description: null).Error);
        Assert.Equal(ComplianceErrors.EldNoticedInFuture, Eld(noticed: Now.AddMinutes(6)).Error);
    }

    [Fact]
    public void Resolving_closes_the_report_once_and_ends_its_overlap()
    {
        var r = Eld().Value;
        r.ClearDomainEvents();

        Assert.True(r.Resolve(OfficerId, Now, "Firmware updated").IsSuccess);
        Assert.False(r.IsOpen);
        Assert.Equal(EldMalfunctionStatus.Resolved, r.Status);
        Assert.Equal(OfficerId, r.ResolvedByUserId);
        Assert.False(r.Overlaps(At("00:00", 1).ToUnixTimeSeconds(), At("00:00", 2).ToUnixTimeSeconds()));
        Assert.IsType<EldMalfunctionResolvedDomainEvent>(Assert.Single(r.DomainEvents));

        r.ClearDomainEvents();
        Assert.True(r.Resolve(OfficerId, Now.AddHours(1), null).IsSuccess);
        Assert.Empty(r.DomainEvents);
    }

    // ---- ComplianceFinding ----

    private static Result<ComplianceFinding> Finding(string? rule = "DailyDriving13h", FindingSeverity severity = FindingSeverity.Major, string? description = "13h42m driving on the audit") =>
        ComplianceFinding.Open(TestDrivers.TenantId, DriverId, D, rule, severity, description, FindingSource.Audit, OfficerId, Now);

    [Theory]
    [InlineData("DailyDriving13h")]
    [InlineData("RecordsIncomplete14d")]
    [InlineData("Falsification")]
    [InlineData("Tampering")]
    [InlineData("TwoRecords")]
    [InlineData("OutOfServiceDeclared")]
    public void A_finding_cites_an_engine_rule_or_a_record_keeping_offence(string rule)
    {
        var result = Finding(rule: rule);

        Assert.True(result.IsSuccess);
        Assert.Equal(rule, result.Value.Rule);
        Assert.Equal(FindingStatus.Open, result.Value.Status);
        Assert.Equal(rule, Assert.IsType<ComplianceFindingOpenedDomainEvent>(Assert.Single(result.Value.DomainEvents)).Rule);
    }

    [Theory]
    [InlineData("dailydriving13h")]
    [InlineData("Speeding")]
    [InlineData("")]
    public void An_unknown_rule_is_refused(string rule) =>
        Assert.Equal(ComplianceErrors.FindingRuleInvalid, Finding(rule: rule).Error);

    [Fact]
    public void A_finding_needs_a_description_and_known_enums()
    {
        Assert.Equal(ComplianceErrors.FindingDescriptionRequired, Finding(description: " ").Error);
        Assert.Equal(ComplianceErrors.FindingSeverityInvalid, Finding(severity: (FindingSeverity)9).Error);
    }

    [Fact]
    public void A_finding_moves_forward_only_and_closed_is_terminal()
    {
        var f = Finding().Value;
        f.ClearDomainEvents();

        Assert.True(f.Update(FindingStatus.Corrected, "Driver coached 2026-09-15", OfficerId, Now).IsSuccess);
        Assert.Equal(FindingStatus.Corrected, f.Status);
        Assert.Equal("Driver coached 2026-09-15", f.FollowUpNote);
        Assert.IsType<ComplianceFindingUpdatedDomainEvent>(Assert.Single(f.DomainEvents));

        Assert.Equal(ComplianceErrors.FindingTransitionInvalid, f.Update(FindingStatus.Open, null, OfficerId, Now).Error);

        f.ClearDomainEvents();
        Assert.True(f.Update(FindingStatus.Closed, "Driver coached 2026-09-15", OfficerId, Now.AddDays(1)).IsSuccess);
        Assert.Equal(Now.AddDays(1), f.ClosedAtUtc);
        Assert.Equal(OfficerId, f.ClosedByUserId);
        Assert.Single(f.DomainEvents);

        Assert.Equal(ComplianceErrors.FindingClosed, f.Update(FindingStatus.Corrected, null, OfficerId, Now).Error);
        Assert.Equal(ComplianceErrors.FindingClosed, f.Update(FindingStatus.Closed, "a different note", OfficerId, Now).Error);
    }

    [Fact]
    public void Updating_nothing_is_a_no_op_and_a_new_note_alone_is_a_change()
    {
        var f = Finding().Value;
        f.ClearDomainEvents();

        Assert.True(f.Update(FindingStatus.Open, null, OfficerId, Now).IsSuccess);
        Assert.Empty(f.DomainEvents);

        Assert.True(f.Update(FindingStatus.Open, "Awaiting the driver's statement", OfficerId, Now).IsSuccess);
        Assert.Single(f.DomainEvents);
        Assert.Equal(FindingStatus.Open, f.Status);
    }

    [Fact]
    public void Open_may_close_directly()
    {
        var f = Finding().Value;

        Assert.True(f.Update(FindingStatus.Closed, null, OfficerId, Now).IsSuccess);
        Assert.Equal(FindingStatus.Closed, f.Status);
    }

    // ---- Driver.AttestPriorRecords ----

    [Fact]
    public void Attesting_prior_records_sets_the_fields_and_raises_its_own_event()
    {
        var driver = RosterDriver();

        var result = driver.AttestPriorRecords(D.AddDays(-1), OfficerId, "Logs from Keewatin obtained", true, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(D.AddDays(-1), driver.PriorRecordsCoveredThroughDate);
        Assert.Equal(Now, driver.PriorRecordsAttestedAtUtc);
        Assert.Equal(OfficerId, driver.PriorRecordsAttestedByUserId);
        Assert.Equal("Logs from Keewatin obtained", driver.PriorRecordsNote);
        Assert.True(driver.WorksForOtherCarriers);
        var raised = Assert.IsType<DriverPriorRecordsAttestedDomainEvent>(Assert.Single(driver.DomainEvents));
        Assert.Equal(D.AddDays(-1), raised.CoveredThroughDate);
        Assert.True(raised.WorksForOtherCarriers);
    }

    [Fact]
    public void Prior_records_can_be_attested_through_today_but_not_beyond()
    {
        var driver = RosterDriver();

        Assert.True(driver.AttestPriorRecords(D, OfficerId, null, false, Now).IsSuccess);
        Assert.Equal(ComplianceErrors.PriorRecordsDateInFuture, driver.AttestPriorRecords(D.AddDays(1), OfficerId, null, false, Now).Error);
        Assert.Equal(DriverErrors.UserIdRequired, driver.AttestPriorRecords(D, Guid.Empty, null, false, Now).Error);
    }

    [Fact]
    public void An_identical_re_attestation_is_a_no_op_and_a_changed_one_is_fresh()
    {
        var driver = RosterDriver();
        Assert.True(driver.AttestPriorRecords(D.AddDays(-1), OfficerId, "note", false, Now).IsSuccess);
        driver.ClearDomainEvents();

        Assert.True(driver.AttestPriorRecords(D.AddDays(-1), Guid.NewGuid(), "note", false, Now.AddHours(1)).IsSuccess);
        Assert.Empty(driver.DomainEvents);
        Assert.Equal(OfficerId, driver.PriorRecordsAttestedByUserId);

        var later = Guid.NewGuid();
        Assert.True(driver.AttestPriorRecords(D, later, "note", false, Now.AddHours(2)).IsSuccess);
        Assert.Single(driver.DomainEvents);
        Assert.Equal(later, driver.PriorRecordsAttestedByUserId);
        Assert.Equal(Now.AddHours(2), driver.PriorRecordsAttestedAtUtc);
    }

    [Fact]
    public void Nothing_here_is_the_engines_rule()
    {
        // A sanity pin: the aggregate vocabulary and the engine's agree on what an exception is
        // called, so the audit grid's rule ids and the finding's rule ids are one set.
        Assert.True(ComplianceFinding.IsKnownRule(HosWire.RuleId(HosRule.Cycle1_70hIn7d)));
    }
}
