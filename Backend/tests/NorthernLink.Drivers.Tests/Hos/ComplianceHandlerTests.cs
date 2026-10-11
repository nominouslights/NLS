using NorthernLink.Drivers.Application.Drivers.AttestPriorRecords;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.Audit;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetRange;
using NorthernLink.Drivers.Application.Hos.EldMalfunctions.Report;
using NorthernLink.Drivers.Application.Hos.EldMalfunctions.Resolve;
using NorthernLink.Drivers.Application.Hos.Exceptions.Acknowledge;
using NorthernLink.Drivers.Application.Hos.Exceptions.ApproveDeferral;
using NorthernLink.Drivers.Application.Hos.Exceptions.Declare;
using NorthernLink.Drivers.Application.Hos.Exceptions.Get;
using NorthernLink.Drivers.Application.Hos.Exceptions.Reject;
using NorthernLink.Drivers.Application.Hos.Exceptions.Withdraw;
using NorthernLink.Drivers.Application.Hos.Findings.Open;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Application.Hos.OutOfService.Lift;
using NorthernLink.Drivers.Application.Hos.OutOfService.Record;
using NorthernLink.Drivers.Application.Hos.Status;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using Xunit;
using static NorthernLink.Drivers.Tests.Hos.HosTestSupport;
using DutyStatus = NorthernLink.Drivers.Domain.Hos.DutyStatus;
using HosException = NorthernLink.Drivers.Domain.Hos.HosException;

namespace NorthernLink.Drivers.Tests.Hos;

/// <summary>
/// The compliance handlers over the in-memory fakes: the role each decision needs (modelled on
/// certification's actor check), the out-of-service one-active rule and the finding it opens,
/// the early-lift reason, and the status / days / audit reads with every compliance record fed
/// to the engine — including the attestation moving the ledger's known-from.
/// </summary>
public class ComplianceHandlerTests
{
    private static readonly Guid OfficerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TripId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly FixedClock Clock = new(Now);
    private static readonly FakeActor Driver = new(UserId, Roles.Driver);
    private static readonly FakeActor Dispatcher = new(Guid.Parse("77777777-7777-7777-7777-777777777777"), Roles.Dispatcher);
    private static readonly FakeActor Officer = new(OfficerId, Roles.ComplianceOfficer);

    private static FakeDutyDayRepository TodayDriving()
    {
        var repository = new FakeDutyDayRepository();
        repository.Days.Add(DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("06:30", DutyStatus.Driving)));
        return repository;
    }

    /// <summary>Fourteen certified days off before today, so the records are complete and only today's hours count.</summary>
    private static FakeDutyDayRepository CompleteRecordsThenDriving()
    {
        var repository = TodayDriving();
        for (var offset = -20; offset <= -1; offset++)
        {
            var day = DutyDay.Open(TestDrivers.TenantId, DriverId, D.AddDays(offset), Now);
            Assert.True(day.Append(Draft("00:00", DutyStatus.OffDuty, dayOffset: offset), Now).IsSuccess);
            Assert.True(day.Certify(UserId, Now).IsSuccess);
            repository.Days.Add(day);
        }

        return repository;
    }

    private static HosException Adverse(FakeComplianceRecords records, string from = "10:00", int minutes = 90)
    {
        var e = HosException.Declare(
            TestDrivers.TenantId, DriverId, HosExceptionKind.AdverseDrivingConditions, UserId, Now,
            At(from), null, "Whiteout", null, true, TimeSpan.FromMinutes(minutes), false, TripId).Value;
        e.ClearDomainEvents();
        records.Exceptions.Add(e);
        return e;
    }

    private static DriverOutOfService OutOfServiceSince(FakeComplianceRecords records, string declared = "14:00")
    {
        var r = DriverOutOfService.Record(TestDrivers.TenantId, DriverId, At(declared), OfficerId, Now, null, false, "Inspection", null).Value;
        r.ClearDomainEvents();
        records.OutOfService.Add(r);
        return r;
    }

    // ---- Deferral approval: ComplianceAccess only ----

    [Fact]
    public async Task A_dispatcher_cannot_approve_a_deferral_and_nothing_is_saved()
    {
        var records = new FakeComplianceRecords();
        var handler = new ApproveOffDutyDeferralCommandHandler(records, new FakeDriverRepository(records.Driver!), Dispatcher, Clock);

        var result = await handler.Handle(
            new ApproveOffDutyDeferralCommand(TestDrivers.TenantId, DriverId, TripId, At("06:00", 1), TimeSpan.FromHours(1), "Late return", null, false),
            CancellationToken.None);

        Assert.Equal(ComplianceErrors.DeferralRequiresComplianceOfficer, result.Error);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Empty(records.Exceptions);
        Assert.Equal(0, records.Saves);
    }

    [Fact]
    public async Task A_compliance_officer_approves_a_deferral_and_it_is_born_approved()
    {
        var records = new FakeComplianceRecords();
        var handler = new ApproveOffDutyDeferralCommandHandler(records, new FakeDriverRepository(records.Driver!), Officer, Clock);

        var result = await handler.Handle(
            new ApproveOffDutyDeferralCommand(TestDrivers.TenantId, DriverId, TripId, At("06:00", 1), TimeSpan.FromHours(1), "Late return", null, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.Equal(HosWire.OffDutyDeferralKind, result.Value.Kind);
        Assert.Equal("Approved", result.Value.Status);
        Assert.True(result.Value.Applying);
        Assert.Equal(60, result.Value.DeferredOffDutyMinutes);
        Assert.Null(result.Value.ExtraDrivingMinutes);
        Assert.Equal(OfficerId, result.Value.ApprovedByUserId);
        Assert.Single(records.Exceptions);
        Assert.Equal(1, records.Saves);
    }

    // ---- Declare / acknowledge / reject / withdraw ----

    [Fact]
    public async Task The_driver_declares_adverse_conditions_under_their_own_id()
    {
        var records = new FakeComplianceRecords();
        var handler = new DeclareHosExceptionCommandHandler(records, new FakeDriverRepository(records.Driver!), Driver, Clock);

        var result = await handler.Handle(
            new DeclareHosExceptionCommand(
                TestDrivers.TenantId, DriverId, HosExceptionKind.AdverseDrivingConditions, At("10:00"), null,
                "Whiteout", "PR 391", TripId, true, TimeSpan.FromMinutes(90), true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.Equal(HosWire.AdverseDrivingConditionsKind, result.Value.Kind);
        Assert.Equal("Declared", result.Value.Status);
        Assert.Equal(90, result.Value.ExtraDrivingMinutes);
        Assert.Equal(UserId, result.Value.DeclaredByUserId);
        Assert.Equal(1, records.Saves);
    }

    [Fact]
    public async Task Declaring_for_an_unknown_driver_is_not_found() =>
        Assert.Equal(
            Domain.Drivers.DriverErrors.NotFound,
            (await new DeclareHosExceptionCommandHandler(new FakeComplianceRecords(), new FakeDriverRepository(), Driver, Clock).Handle(
                new DeclareHosExceptionCommand(
                    TestDrivers.TenantId, DriverId, HosExceptionKind.Emergency, At("10:00"), null, "Medical", null, null, false, null, false),
                CancellationToken.None)).Error);

    [Fact]
    public async Task Only_dispatch_or_compliance_acknowledges()
    {
        var records = new FakeComplianceRecords();
        var e = Adverse(records);

        var byDriver = await new AcknowledgeHosExceptionCommandHandler(records, Driver, Clock)
            .Handle(new AcknowledgeHosExceptionCommand(TestDrivers.TenantId, DriverId, e.Id), CancellationToken.None);
        Assert.Equal(ComplianceErrors.AcknowledgeRequiresDispatch, byDriver.Error);

        var byDispatch = await new AcknowledgeHosExceptionCommandHandler(records, Dispatcher, Clock)
            .Handle(new AcknowledgeHosExceptionCommand(TestDrivers.TenantId, DriverId, e.Id), CancellationToken.None);
        Assert.True(byDispatch.IsSuccess);
        Assert.Equal("Acknowledged", byDispatch.Value.Status);
        Assert.Equal(Dispatcher.UserId, byDispatch.Value.DispatchAcknowledgedByUserId);
    }

    [Fact]
    public async Task Only_compliance_rejects_and_a_rejected_exception_stops_applying()
    {
        var records = new FakeComplianceRecords();
        var e = Adverse(records);

        Assert.Equal(
            ComplianceErrors.ComplianceOfficerRequired,
            (await new RejectHosExceptionCommandHandler(records, Dispatcher, Clock)
                .Handle(new RejectHosExceptionCommand(TestDrivers.TenantId, DriverId, e.Id, null), CancellationToken.None)).Error);

        var rejected = await new RejectHosExceptionCommandHandler(records, Officer, Clock)
            .Handle(new RejectHosExceptionCommand(TestDrivers.TenantId, DriverId, e.Id, "Forecast was public"), CancellationToken.None);
        Assert.True(rejected.IsSuccess);
        Assert.Equal("Rejected", rejected.Value.Status);
        Assert.False(rejected.Value.Applying);
        Assert.Equal("Forecast was public", rejected.Value.ClosureNote);
    }

    [Fact]
    public async Task The_driver_withdraws_their_own_declared_exception_but_not_an_acknowledged_one()
    {
        var records = new FakeComplianceRecords();
        var e = Adverse(records);
        Assert.True(e.Acknowledge(Dispatcher.UserId!.Value, Now).IsSuccess);

        var byDriver = await new WithdrawHosExceptionCommandHandler(records, Driver, Clock)
            .Handle(new WithdrawHosExceptionCommand(TestDrivers.TenantId, DriverId, e.Id, null), CancellationToken.None);
        Assert.Equal(ComplianceErrors.ExceptionWithdrawRequiresComplianceOfficer, byDriver.Error);

        var byOfficer = await new WithdrawHosExceptionCommandHandler(records, Officer, Clock)
            .Handle(new WithdrawHosExceptionCommand(TestDrivers.TenantId, DriverId, e.Id, "driver asked"), CancellationToken.None);
        Assert.True(byOfficer.IsSuccess);
        Assert.Equal("Withdrawn", byOfficer.Value.Status);
    }

    [Fact]
    public async Task An_exception_on_another_drivers_record_is_not_found()
    {
        var records = new FakeComplianceRecords();
        var e = Adverse(records);

        var result = await new WithdrawHosExceptionCommandHandler(records, Officer, Clock)
            .Handle(new WithdrawHosExceptionCommand(TestDrivers.TenantId, Guid.NewGuid(), e.Id, null), CancellationToken.None);

        Assert.Equal(ComplianceErrors.ExceptionNotFound, result.Error);
    }

    [Fact]
    public async Task The_exceptions_list_covers_the_requested_days_at_every_status()
    {
        var records = new FakeComplianceRecords();
        var today = Adverse(records);
        var old = HosException.Declare(
            TestDrivers.TenantId, DriverId, HosExceptionKind.Emergency, UserId, At("12:00", -5),
            At("10:00", -5), At("11:00", -5), "Medical", null, false, null, false, null).Value;
        records.Exceptions.Add(old);
        Assert.True(today.Reject(OfficerId, Now, null).IsSuccess);
        var handler = new GetHosExceptionsQueryHandler(records);

        var recent = (await handler.Handle(new GetHosExceptionsQuery(TestDrivers.TenantId, DriverId, D.AddDays(-1), D), CancellationToken.None)).Value;
        Assert.Equal([today.Id], recent.Select(e => e.Id));
        Assert.Equal("Rejected", recent[0].Status);

        var all = (await handler.Handle(new GetHosExceptionsQuery(TestDrivers.TenantId, DriverId, D.AddDays(-13), D), CancellationToken.None)).Value;
        Assert.Equal([old.Id, today.Id], all.Select(e => e.Id));

        Assert.Equal(
            GetDutyDaysQueryHandler.RangeInvalid,
            (await handler.Handle(new GetHosExceptionsQuery(TestDrivers.TenantId, DriverId, D, D.AddDays(-1)), CancellationToken.None)).Error);
    }

    // ---- Out of service ----

    private static RecordOutOfServiceCommand RecordOos(string declared = "14:00") =>
        new(TestDrivers.TenantId, DriverId, At(declared), null, false, "Roadside inspection: logbook not produced", "MPI 44-1021");

    [Fact]
    public async Task Recording_out_of_service_takes_a_compliance_officer_and_opens_a_severe_finding_in_the_same_save()
    {
        var records = new FakeComplianceRecords();
        var drivers = new FakeDriverRepository(records.Driver!);

        Assert.Equal(
            ComplianceErrors.ComplianceOfficerRequired,
            (await new RecordOutOfServiceCommandHandler(records, records, drivers, Dispatcher, Clock).Handle(RecordOos(), CancellationToken.None)).Error);
        Assert.Empty(records.OutOfService);

        var result = await new RecordOutOfServiceCommandHandler(records, records, drivers, Officer, Clock).Handle(RecordOos(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.Equal("Active", result.Value.Status);
        Assert.True(result.Value.Active);
        Assert.Equal(At("14:00").AddHours(72), result.Value.UntilUtc);
        Assert.Equal(1, records.Saves);

        var finding = Assert.Single(records.Findings);
        Assert.Equal(ComplianceFinding.OutOfServiceDeclaredRule, finding.Rule);
        Assert.Equal(FindingSeverity.Severe, finding.Severity);
        Assert.Equal(FindingSource.Auto, finding.Source);
        Assert.Equal(D, finding.LocalDate);
        Assert.Equal(OfficerId, finding.OpenedByUserId);
        Assert.Contains("logbook not produced", finding.Description);
    }

    [Fact]
    public async Task At_most_one_active_declaration_per_driver()
    {
        var records = new FakeComplianceRecords();
        var handler = new RecordOutOfServiceCommandHandler(records, records, new FakeDriverRepository(records.Driver!), Officer, Clock);
        Assert.True((await handler.Handle(RecordOos(), CancellationToken.None)).IsSuccess);

        var second = await handler.Handle(RecordOos("14:30"), CancellationToken.None);

        Assert.Equal(ComplianceErrors.AlreadyOutOfService, second.Error);
        Assert.Single(records.OutOfService);
        Assert.Single(records.Findings);
    }

    [Fact]
    public async Task An_expired_but_unlifted_declaration_does_not_block_a_new_one()
    {
        var records = new FakeComplianceRecords();
        var expired = DriverOutOfService.Record(
            TestDrivers.TenantId, DriverId, At("14:00", -10), OfficerId, At("15:00", -10), null, false, "Old", null).Value;
        expired.ClearDomainEvents();
        records.OutOfService.Add(expired);
        var handler = new RecordOutOfServiceCommandHandler(records, records, new FakeDriverRepository(records.Driver!), Officer, Clock);

        Assert.True((await handler.Handle(RecordOos(), CancellationToken.None)).IsSuccess);
        Assert.Equal(2, records.OutOfService.Count);
    }

    [Fact]
    public async Task Lifting_early_needs_a_reason_and_a_compliance_officer()
    {
        var records = new FakeComplianceRecords();
        var r = OutOfServiceSince(records);

        Assert.Equal(
            ComplianceErrors.ComplianceOfficerRequired,
            (await new LiftOutOfServiceCommandHandler(records, Dispatcher, Clock)
                .Handle(new LiftOutOfServiceCommand(TestDrivers.TenantId, DriverId, r.Id, "x"), CancellationToken.None)).Error);

        var handler = new LiftOutOfServiceCommandHandler(records, Officer, Clock);
        Assert.Equal(
            ComplianceErrors.LiftReasonRequired,
            (await handler.Handle(new LiftOutOfServiceCommand(TestDrivers.TenantId, DriverId, r.Id, null), CancellationToken.None)).Error);
        Assert.Equal(0, records.Saves);

        var lifted = await handler.Handle(new LiftOutOfServiceCommand(TestDrivers.TenantId, DriverId, r.Id, "Logbook produced"), CancellationToken.None);
        Assert.True(lifted.IsSuccess);
        Assert.Equal("Lifted", lifted.Value.Status);
        Assert.False(lifted.Value.Active);
        Assert.Equal(Now, lifted.Value.EffectiveUntilUtc);
        Assert.Equal(1, records.Saves);
    }

    // ---- ELD malfunctions ----

    [Fact]
    public async Task An_open_malfunction_shows_on_the_status_until_compliance_resolves_it()
    {
        var records = new FakeComplianceRecords();
        var reported = await new ReportEldMalfunctionCommandHandler(records, new FakeDriverRepository(records.Driver!), Driver, Clock).Handle(
            new ReportEldMalfunctionCommand(TestDrivers.TenantId, DriverId, null, "P", At("08:00"), "Positioning malfunction light"),
            CancellationToken.None);
        Assert.True(reported.IsSuccess, reported.IsFailure ? reported.Error.Code : null);
        Assert.Equal("Open", reported.Value.Status);

        var status = new GetHosStatusQueryHandler(TodayDriving(), records, Clock);
        Assert.True((await status.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value.EldMalfunctionOpen);

        Assert.Equal(
            ComplianceErrors.ComplianceOfficerRequired,
            (await new ResolveEldMalfunctionCommandHandler(records, Dispatcher, Clock).Handle(
                new ResolveEldMalfunctionCommand(TestDrivers.TenantId, DriverId, reported.Value.Id, null), CancellationToken.None)).Error);

        var resolved = await new ResolveEldMalfunctionCommandHandler(records, Officer, Clock).Handle(
            new ResolveEldMalfunctionCommand(TestDrivers.TenantId, DriverId, reported.Value.Id, "Firmware"), CancellationToken.None);
        Assert.True(resolved.IsSuccess);
        Assert.Equal("Resolved", resolved.Value.Status);
        Assert.False((await status.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value.EldMalfunctionOpen);
    }

    // ---- Findings ----

    [Fact]
    public async Task Opening_a_finding_takes_a_compliance_officer()
    {
        var records = new FakeComplianceRecords();
        var drivers = new FakeDriverRepository(records.Driver!);
        var command = new OpenComplianceFindingCommand(
            TestDrivers.TenantId, DriverId, D, "DailyDriving13h", FindingSeverity.Major, "13h42m on the audit", FindingSource.Audit, null);

        Assert.Equal(
            ComplianceErrors.ComplianceOfficerRequired,
            (await new OpenComplianceFindingCommandHandler(records, drivers, Dispatcher, Clock).Handle(command, CancellationToken.None)).Error);

        var result = await new OpenComplianceFindingCommandHandler(records, drivers, Officer, Clock).Handle(command, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("Open", result.Value.Status);
        Assert.Equal("Major", result.Value.Severity);
        Assert.Equal(OfficerId, result.Value.OpenedByUserId);
        Assert.Single(records.Findings);
    }

    // ---- Attestation and the ledger's known-from ----

    [Fact]
    public async Task Attesting_prior_records_moves_the_known_from_back_and_the_records_become_complete()
    {
        var records = new FakeComplianceRecords();
        var drivers = new FakeDriverRepository(records.Driver!);
        var status = new GetHosStatusQueryHandler(TodayDriving(), records, Clock);

        var before = (await status.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value;
        Assert.False(before.RecordsOnFile);
        Assert.Equal(At("00:00"), before.KnownFromUtc);
        Assert.Null(before.PriorRecords);

        Assert.Equal(
            ComplianceErrors.ComplianceOfficerRequired,
            (await new AttestPriorRecordsCommandHandler(drivers, Dispatcher, Clock).Handle(
                new AttestPriorRecordsCommand(TestDrivers.TenantId, DriverId, D.AddDays(-1), null, false), CancellationToken.None)).Error);

        var attested = await new AttestPriorRecordsCommandHandler(drivers, Officer, Clock).Handle(
            new AttestPriorRecordsCommand(TestDrivers.TenantId, DriverId, D.AddDays(-1), "Keewatin logs on file", true), CancellationToken.None);
        Assert.True(attested.IsSuccess, attested.IsFailure ? attested.Error.Code : null);
        Assert.Equal(D.AddDays(-1), attested.Value.CoveredThroughDate);
        Assert.Equal(OfficerId, attested.Value.AttestedByUserId);
        Assert.True(attested.Value.WorksForOtherCarriers);
        Assert.Equal(1, drivers.Saves);

        var after = (await status.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value;
        Assert.True(after.RecordsOnFile);
        Assert.Equal(At("00:00", -14), after.KnownFromUtc);
        Assert.DoesNotContain(BlockingReason.RecordsIncomplete, after.Evaluation.BlockingReasons);
        Assert.True(after.Evaluation.CanDriveNow);
        Assert.NotNull(after.PriorRecords);
        Assert.Equal("Keewatin logs on file", after.PriorRecords!.Note);
        Assert.True(after.PriorRecords.WorksForOtherCarriers);
    }

    [Fact]
    public void The_assembler_feeds_attested_days_as_declared_days_only_before_the_drivers_own_records()
    {
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty));
        var records = new FakeComplianceRecords();
        Assert.True(records.Driver!.AttestPriorRecords(D.AddDays(3), OfficerId, null, false, At("00:00", 4)).IsSuccess);

        var ledger = DutyLedgerAssembler.Build(DriverId, [day], D, Now.ToUnixTimeSeconds(), records.Records(DriverId));

        // Covered [D-10, D+3]; only the days before D (the first own day) become rows.
        Assert.Equal(DutyLedgerAssembler.Calendar.DayStartUnix(D.AddDays(-10)), ledger.KnownFromUnix);
        Assert.Equal(10, ledger.OtherCarrierDays.Count);
        Assert.All(ledger.OtherCarrierDays, r => Assert.True(r.Date < D));
        Assert.All(ledger.OtherCarrierDays, r => Assert.Equal(DutyLedgerAssembler.PriorRecordsCarrierName, r.CarrierName));
        Assert.All(ledger.OtherCarrierDays, r => Assert.Equal(0, r.OnDutySeconds));
    }

    // ---- The engine sees every compliance record ----

    [Fact]
    public async Task An_active_out_of_service_declaration_blocks_the_status_and_names_the_record()
    {
        var records = new FakeComplianceRecords();
        var r = OutOfServiceSince(records);
        var handler = new GetHosStatusQueryHandler(CompleteRecordsThenDriving(), records, Clock);

        var status = (await handler.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value;

        Assert.NotNull(status.OutOfService);
        Assert.Equal(r.Id, status.OutOfService!.Id);
        Assert.Equal(At("14:00"), status.OutOfService.FromUtc);
        Assert.Equal(At("14:00").AddHours(72), status.OutOfService.UntilUtc);
        Assert.Equal("Inspection", status.OutOfService.Reason);
        Assert.False(status.Evaluation.CanDriveNow);
        Assert.Contains(BlockingReason.OutOfService, status.Evaluation.BlockingReasons);
        Assert.Contains(status.Evaluation.Violations, v => v.Rule == HosRule.DroveWhileOutOfService);

        // The roster batch sees it too.
        var roster = new GetHosRosterStatusQueryHandler(CompleteRecordsThenDriving(), records, new FakeDriverReadService(Roster(DriverId)), Clock);
        var row = Assert.Single((await roster.Handle(new GetHosRosterStatusQuery(TestDrivers.TenantId), CancellationToken.None)).Value);
        Assert.Contains(BlockingReason.OutOfService, row.BlockingReasons);
    }

    [Fact]
    public async Task A_lifted_declaration_no_longer_blocks()
    {
        var records = new FakeComplianceRecords();
        var r = OutOfServiceSince(records, "06:00");
        Assert.True(r.Lift(OfficerId, At("07:00"), "cleared").IsSuccess);
        var handler = new GetHosStatusQueryHandler(CompleteRecordsThenDriving(), records, Clock);

        var status = (await handler.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value;

        Assert.Null(status.OutOfService);
        Assert.DoesNotContain(BlockingReason.OutOfService, status.Evaluation.BlockingReasons);
    }

    [Fact]
    public async Task An_adverse_conditions_exception_extends_todays_ceilings_and_a_rejected_one_does_not()
    {
        var records = new FakeComplianceRecords();
        var e = Adverse(records, minutes: 90);
        var handler = new GetHosStatusQueryHandler(CompleteRecordsThenDriving(), records, Clock);

        var extended = (await handler.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value;
        Assert.Equal(90 * 60, extended.Evaluation.Shift.AdverseExtensionSeconds);
        Assert.Equal(13 * 3600 + 90 * 60 - (8 * 3600 + 30 * 60), extended.Evaluation.RemainingDrivingTodaySeconds);

        Assert.True(e.Reject(OfficerId, Now, null).IsSuccess);
        var plain = (await handler.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value;
        Assert.Equal(0, plain.Evaluation.Shift.AdverseExtensionSeconds);
        Assert.Equal(13 * 3600 - (8 * 3600 + 30 * 60), plain.Evaluation.RemainingDrivingTodaySeconds);
    }

    [Fact]
    public async Task Day_summaries_carry_the_days_applying_exceptions_and_its_out_of_service_overlap()
    {
        var records = new FakeComplianceRecords();
        var applying = Adverse(records);
        var rejected = Adverse(records, from: "11:00", minutes: 30);
        Assert.True(rejected.Reject(OfficerId, Now, null).IsSuccess);
        var oos = OutOfServiceSince(records);
        var handler = new GetDutyDaysQueryHandler(TodayDriving(), records, Clock);

        var summaries = (await handler.Handle(new GetDutyDaysQuery(TestDrivers.TenantId, DriverId, D.AddDays(-1), D), CancellationToken.None)).Value;

        Assert.Empty(summaries[0].Exceptions);
        Assert.Null(summaries[0].OutOfService);
        Assert.Equal([applying.Id], summaries[1].Exceptions.Select(e => e.Id));
        Assert.Equal(oos.Id, summaries[1].OutOfService!.Id);
    }

    // ---- The audit ----

    [Fact]
    public async Task The_audit_row_carries_everything_that_bears_on_the_day()
    {
        var records = new FakeComplianceRecords();
        var repository = new FakeDutyDayRepository();
        repository.Days.Add(DayWith(
            Draft("06:00", DutyStatus.OnDuty),
            Draft("06:30", DutyStatus.Driving, source: DutyEventSource.OtherCarrier, carrierName: "Keewatin Rail Partners"),
            Draft("12:00", DutyStatus.OffDuty, source: DutyEventSource.PaperLog, enteredBy: "D. Wells")));
        var applying = Adverse(records);
        var rejected = Adverse(records, from: "11:00", minutes: 30);
        Assert.True(rejected.Reject(OfficerId, Now, null).IsSuccess);
        var oos = OutOfServiceSince(records);
        records.Malfunctions.Add(EldMalfunctionReport.Report(TestDrivers.TenantId, DriverId, "J. Spence", null, "P", At("08:00"), UserId, Now, "Light on").Value);
        records.Findings.Add(ComplianceFinding.Open(TestDrivers.TenantId, DriverId, D, "Falsification", FindingSeverity.Severe, "Two logbooks", FindingSource.Enforcement, OfficerId, Now).Value);
        records.Findings.Add(ComplianceFinding.Open(TestDrivers.TenantId, DriverId, D.AddDays(-3), "TwoRecords", FindingSeverity.Minor, "Earlier", FindingSource.Audit, OfficerId, Now).Value);
        Assert.True(records.Driver!.AttestPriorRecords(D.AddDays(-1), OfficerId, null, false, Now).IsSuccess);
        var handler = new GetHosAuditQueryHandler(repository, records, Clock);

        var rows = (await handler.Handle(new GetHosAuditQuery(TestDrivers.TenantId, DriverId, D.AddDays(-1), D), CancellationToken.None)).Value;

        Assert.Equal([D.AddDays(-1), D], rows.Select(r => r.Date));

        var yesterday = rows[0];
        Assert.False(yesterday.HasRecords);
        Assert.Equal(0, yesterday.EventCount);
        Assert.Empty(yesterday.Sources);
        Assert.True(yesterday.PriorRecordsAttested);
        Assert.Empty(yesterday.Findings);
        Assert.Empty(yesterday.Exceptions);
        Assert.False(yesterday.EldMalfunctionOpen);
        Assert.Null(yesterday.OutOfService);

        var today = rows[1];
        Assert.True(today.HasRecords);
        Assert.Equal(3, today.EventCount);
        Assert.Equal([HosWire.DriverAppSource, HosWire.OtherCarrierSource, HosWire.PaperLogSource], today.Sources);
        Assert.False(today.Certified);
        Assert.Equal(5 * 3600 + 30 * 60, today.Totals.DrivingSeconds);
        Assert.False(today.PriorRecordsAttested);
        Assert.Equal([applying.Id, rejected.Id], today.Exceptions.Select(e => e.Id));
        Assert.Equal(["Declared", "Rejected"], today.Exceptions.Select(e => e.Status));
        Assert.Equal(oos.Id, today.OutOfService!.Id);
        Assert.True(today.OutOfService.Active);
        Assert.True(today.EldMalfunctionOpen);
        var carrier = Assert.Single(today.OtherCarrier);
        Assert.Equal(("Keewatin Rail Partners", 1), (carrier.CarrierName, carrier.EventCount));
        var finding = Assert.Single(today.Findings);
        Assert.Equal("Falsification", finding.Rule);
        Assert.Equal("Enforcement", finding.Source);
        Assert.Equal(HosDisplay.PlanningLabel, today.Label);
    }

    [Fact]
    public async Task The_audit_refuses_a_bad_range_and_an_unknown_driver()
    {
        var known = new GetHosAuditQueryHandler(new FakeDutyDayRepository(), new FakeComplianceRecords(), Clock);
        Assert.Equal(
            GetDutyDaysQueryHandler.RangeInvalid,
            (await known.Handle(new GetHosAuditQuery(TestDrivers.TenantId, DriverId, D, D.AddDays(-1)), CancellationToken.None)).Error);

        var unknown = new GetHosAuditQueryHandler(new FakeDutyDayRepository(), new FakeComplianceRecords { Driver = null }, Clock);
        Assert.Equal(
            Domain.Drivers.DriverErrors.NotFound,
            (await unknown.Handle(new GetHosAuditQuery(TestDrivers.TenantId, DriverId, D.AddDays(-1), D), CancellationToken.None)).Error);
    }
}
