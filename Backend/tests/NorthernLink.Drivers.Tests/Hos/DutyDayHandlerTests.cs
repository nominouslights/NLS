using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.DutyDays.Certify;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetOne;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetRange;
using NorthernLink.Drivers.Application.Hos.DutyDays.Transcribe;
using NorthernLink.Drivers.Application.Hos.DutyEvents.Amend;
using NorthernLink.Drivers.Application.Hos.DutyEvents.Record;
using NorthernLink.Drivers.Application.Hos.Status;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using Xunit;
using static NorthernLink.Drivers.Tests.Hos.HosTestSupport;
using DutyStatus = NorthernLink.Drivers.Domain.Hos.DutyStatus;

namespace NorthernLink.Drivers.Tests.Hos;

/// <summary>
/// The ledger's handlers over the in-memory fakes: the 201/200 semantics of a duty-event
/// write, transcription's all-or-nothing and retry behaviour, certification being the driver's
/// own act, and the reads assembling the engine's ledger and labelling every figure.
/// </summary>
public class DutyDayHandlerTests
{
    private static readonly FixedClock Clock = new(Now);
    private static readonly FakeActor Driver = new(UserId, Roles.Driver);

    /// <summary>Nothing on the compliance side — the reads see a known driver with no records.</summary>
    private static FakeComplianceRecords NoCompliance() => new();

    private static RecordDutyEventCommand Record(
        string hhmm, DutyStatus status, Guid? eventId = null, string source = HosDisplay.DriverAppSource) =>
        new(
            TestDrivers.TenantId, DriverId, eventId, At(hhmm), status,
            HosDisplay.SourceFromWire(source).Value,
            null, null, null, null, null, false, false, false);

    // ---- POST duty-events ----

    [Fact]
    public async Task The_first_write_opens_the_day_saves_and_reports_created()
    {
        var repository = new FakeDutyDayRepository();
        var handler = new RecordDutyEventCommandHandler(repository, Driver, Clock);
        var id = Guid.NewGuid();

        var result = await handler.Handle(Record("06:00", DutyStatus.OnDuty, id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        Assert.Equal(id, result.Value.EventId);
        Assert.Equal(D, result.Value.LocalDate);
        var day = Assert.Single(repository.Days);
        Assert.Equal(result.Value.DutyDayId, day.Id);
        Assert.Equal(1, repository.Saves);
        Assert.Equal(UserId, day.Events[0].RecordedByUserId);
    }

    /// <summary>201 then 200 with the identical body, and the second call saves nothing.</summary>
    [Fact]
    public async Task A_replay_reports_not_created_with_the_same_ids_and_saves_nothing()
    {
        var repository = new FakeDutyDayRepository();
        var handler = new RecordDutyEventCommandHandler(repository, Driver, Clock);
        var command = Record("06:00", DutyStatus.OnDuty, Guid.NewGuid());

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command with { AtUtc = At("09:00"), Status = DutyStatus.Driving }, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.False(second.Value.Created);
        Assert.Equal(first.Value.EventId, second.Value.EventId);
        Assert.Equal(first.Value.DutyDayId, second.Value.DutyDayId);
        Assert.Equal(1, repository.Saves);
        Assert.Single(repository.Days[0].Events);
    }

    [Fact]
    public async Task The_day_is_the_local_date_of_the_instant_not_the_utc_date()
    {
        var repository = new FakeDutyDayRepository();
        var handler = new RecordDutyEventCommandHandler(repository, Driver, Clock);

        // 23:30 CDT on D is 04:30 UTC on D+1.
        var result = await handler.Handle(Record("23:30", DutyStatus.OffDuty) with { AtUtc = At("23:30").AddHours(-16) }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(D, result.Value.LocalDate);
    }

    [Fact]
    public async Task An_unknown_driver_is_not_found()
    {
        var handler = new RecordDutyEventCommandHandler(new FakeDutyDayRepository(driverExists: false), Driver, Clock);

        var result = await handler.Handle(Record("06:00", DutyStatus.OnDuty), CancellationToken.None);

        Assert.Equal(DriverErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task A_domain_refusal_saves_nothing()
    {
        var repository = new FakeDutyDayRepository();
        var handler = new RecordDutyEventCommandHandler(repository, Driver, Clock);

        var result = await handler.Handle(Record("16:00", DutyStatus.OnDuty), CancellationToken.None);

        Assert.Equal(HosErrors.EventInFuture, result.Error);
        Assert.Empty(repository.Days);
        Assert.Equal(0, repository.Saves);
    }

    // ---- POST days/{date}/transcribe ----

    private static TranscribeDutyDayCommand Transcription(Guid? transcriptionId = null, string source = HosDisplay.EldTranscriptionSource) =>
        new(
            TestDrivers.TenantId, DriverId, D, transcriptionId ?? Guid.NewGuid(),
            HosDisplay.SourceFromWire(source).Value, "D. Wells", null,
            [
                new TranscriptionEntry(new TimeOnly(6, 0), DutyStatus.OnDuty),
                new TranscriptionEntry(new TimeOnly(6, 30), DutyStatus.Driving),
                new TranscriptionEntry(new TimeOnly(12, 0), DutyStatus.OffDuty),
            ]);

    [Fact]
    public async Task A_transcription_appends_every_entry_with_deterministic_ids()
    {
        var repository = new FakeDutyDayRepository();
        var handler = new TranscribeDutyDayCommandHandler(repository, new FakeActor(Guid.NewGuid(), Roles.Dispatcher), Clock);
        var transcriptionId = Guid.NewGuid();

        var result = await handler.Handle(Transcription(transcriptionId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Appended);
        Assert.Equal(0, result.Value.AlreadyRecorded);
        var day = Assert.Single(repository.Days);
        Assert.Equal(
            [TranscriptionEventIds.For(transcriptionId, 0), TranscriptionEventIds.For(transcriptionId, 1), TranscriptionEventIds.For(transcriptionId, 2)],
            day.Events.Select(e => e.EventId));
        Assert.All(day.Events, e => Assert.Equal(DutyEventSource.EldTranscription, e.Source));
        Assert.All(day.Events, e => Assert.Equal("D. Wells", e.EnteredBy));
        Assert.Equal(At("06:30"), day.Events[1].AtUtc);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task Resending_the_same_transcription_is_a_no_op()
    {
        var repository = new FakeDutyDayRepository();
        var handler = new TranscribeDutyDayCommandHandler(repository, new FakeActor(Guid.NewGuid(), Roles.Dispatcher), Clock);
        var command = Transcription();

        await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(0, second.Value.Appended);
        Assert.Equal(3, second.Value.AlreadyRecorded);
        Assert.Equal(1, repository.Saves);
        Assert.Equal(3, repository.Days[0].Events.Count);
        Assert.Empty(repository.Days[0].DomainEvents);
    }

    [Fact]
    public async Task A_transcription_is_all_or_nothing()
    {
        var repository = new FakeDutyDayRepository();
        var handler = new TranscribeDutyDayCommandHandler(repository, new FakeActor(Guid.NewGuid(), Roles.Dispatcher), Clock);
        var bad = Transcription() with
        {
            Entries =
            [
                new TranscriptionEntry(new TimeOnly(6, 0), DutyStatus.OnDuty),
                new TranscriptionEntry(new TimeOnly(7, 0), DutyStatus.OnDuty),
            ],
        };

        var result = await handler.Handle(bad, CancellationToken.None);

        Assert.Equal(HosErrors.NoStatusChange, result.Error);
        Assert.Empty(repository.Days);
        Assert.Equal(0, repository.Saves);
    }

    [Theory]
    [InlineData(HosDisplay.DriverAppSource)]
    [InlineData(HosDisplay.OtherCarrierSource)]
    public async Task A_transcription_must_come_from_the_eld_or_paper(string source)
    {
        var handler = new TranscribeDutyDayCommandHandler(new FakeDutyDayRepository(), Driver, Clock);

        var result = await handler.Handle(Transcription(source: source), CancellationToken.None);

        Assert.Equal(HosErrors.TranscriptionSourceInvalid, result.Error);
    }

    [Fact]
    public async Task Transcription_entries_must_be_chronological()
    {
        var handler = new TranscribeDutyDayCommandHandler(new FakeDutyDayRepository(), Driver, Clock);
        var command = Transcription() with
        {
            Entries = [new TranscriptionEntry(new TimeOnly(7, 0), DutyStatus.OnDuty), new TranscriptionEntry(new TimeOnly(7, 0), DutyStatus.Driving)],
        };

        Assert.Equal(HosErrors.TranscriptionNotChronological, (await handler.Handle(command, CancellationToken.None)).Error);
    }

    // ---- POST duty-events/{id}/amend ----

    [Fact]
    public async Task An_amendment_finds_the_day_by_the_replacements_instant()
    {
        var repository = new FakeDutyDayRepository();
        var original = Guid.NewGuid();
        repository.Days.Add(DayWith(Draft("06:00", DutyStatus.OnDuty, eventId: original)));
        var handler = new AmendDutyEventCommandHandler(repository, new FakeActor(Guid.NewGuid(), Roles.Dispatcher), Clock);

        var result = await handler.Handle(
            new AmendDutyEventCommand(
                TestDrivers.TenantId, DriverId, original, null, At("06:15"), DutyStatus.OnDuty, DutyEventSource.EldTranscription,
                "D. Wells", null, null, null, null, false, false, false, "ELD shows 06:15"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        Assert.Equal(original, result.Value.OriginalEventId);
        Assert.Equal(1, repository.Saves);

        // Same original, a replacement on another day: that day has no such event.
        var elsewhere = await handler.Handle(
            new AmendDutyEventCommand(
                TestDrivers.TenantId, DriverId, original, null, At("06:15", -1), DutyStatus.OnDuty, DutyEventSource.EldTranscription,
                "D. Wells", null, null, null, null, false, false, false, "x"),
            CancellationToken.None);
        Assert.Equal(HosErrors.EventNotFound, elsewhere.Error);
    }

    // ---- POST days/{date}/certify ----

    [Fact]
    public async Task The_driver_certifies_their_day()
    {
        var repository = new FakeDutyDayRepository();
        repository.Days.Add(DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("14:00", DutyStatus.OffDuty)));
        var handler = new CertifyDutyDayCommandHandler(repository, Driver, Clock);

        var result = await handler.Handle(new CertifyDutyDayCommand(TestDrivers.TenantId, DriverId, D), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, repository.Days[0].CertifiedByUserId);
        Assert.Equal(1, repository.Saves);
    }

    /// <summary>
    /// No principal means no driver, and the endpoint's own-driver check (no override) is what
    /// keeps a dispatcher out — see the gateway's metadata pin on the certify route.
    /// </summary>
    [Fact]
    public async Task Certifying_with_no_driver_principal_is_refused()
    {
        var repository = new FakeDutyDayRepository();
        repository.Days.Add(DayWith(Draft("06:00", DutyStatus.OnDuty)));
        var handler = new CertifyDutyDayCommandHandler(repository, new FakeActor(null), Clock);

        var result = await handler.Handle(new CertifyDutyDayCommand(TestDrivers.TenantId, DriverId, D), CancellationToken.None);

        Assert.Equal(HosErrors.CertifyDriverOnly, result.Error);
        Assert.Null(repository.Days[0].CertifiedAtUtc);
    }

    [Fact]
    public async Task Certifying_a_day_with_no_record_is_not_found()
    {
        var handler = new CertifyDutyDayCommandHandler(new FakeDutyDayRepository(), Driver, Clock);

        var result = await handler.Handle(new CertifyDutyDayCommand(TestDrivers.TenantId, DriverId, D), CancellationToken.None);

        Assert.Equal(HosErrors.DayNotFound, result.Error);
    }

    // ---- GET hos/status ----

    [Fact]
    public async Task The_status_query_assembles_the_ledger_evaluates_it_and_labels_the_figures()
    {
        var repository = new FakeDutyDayRepository();
        repository.Days.Add(DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("06:30", DutyStatus.Driving)));
        var handler = new GetHosStatusQueryHandler(repository, NoCompliance(), Clock);

        var result = await handler.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var status = result.Value;
        Assert.Equal(HosDisplay.PlanningLabel, status.Label);
        Assert.Equal(DriverId, status.DriverId);
        Assert.Equal(HosWire.Driving, status.CurrentDuty);
        Assert.Equal(At("06:30"), status.CurrentDutySinceUtc);
        Assert.Equal(Now.ToUnixTimeSeconds(), status.Evaluation.AsOfUnix);
        Assert.Equal(8 * 3600 + 30 * 60, status.Evaluation.Today.DrivingSeconds);
        Assert.Null(status.OutOfService);
        Assert.False(status.EldMalfunctionOpen);
        Assert.Null(status.PriorRecords);

        // One day on file: the ledger's known-from is that day's start, inside the 14-day
        // window, so the records are incomplete and nothing here is a free pass.
        Assert.Equal(At("00:00"), status.KnownFromUtc);
        Assert.False(status.RecordsOnFile);
        Assert.Contains(BlockingReason.RecordsIncomplete, status.Evaluation.BlockingReasons);
        Assert.False(status.Evaluation.CanDriveNow);
    }

    [Fact]
    public async Task A_driver_with_no_ledger_has_no_records_and_nothing_known()
    {
        var handler = new GetHosStatusQueryHandler(new FakeDutyDayRepository(), NoCompliance(), Clock);

        var result = await handler.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.KnownFromUtc);
        Assert.False(result.Value.RecordsOnFile);
        Assert.Equal(HosBand.Off, result.Value.Evaluation.Band);
        Assert.Contains(BlockingReason.NoLedger, result.Value.Evaluation.BlockingReasons);
        Assert.Equal(HosWire.OffDuty, result.Value.CurrentDuty);
    }

    [Fact]
    public async Task Fourteen_certified_days_on_file_make_the_records_complete()
    {
        var repository = new FakeDutyDayRepository();
        for (var offset = -20; offset <= -1; offset++)
        {
            var day = DutyDay.Open(TestDrivers.TenantId, DriverId, D.AddDays(offset), Now);
            Assert.True(day.Append(Draft("00:00", DutyStatus.OffDuty, dayOffset: offset), Now).IsSuccess);
            Assert.True(day.Certify(UserId, Now).IsSuccess);
            repository.Days.Add(day);
        }

        repository.Days.Add(DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("06:30", DutyStatus.Driving)));
        var handler = new GetHosStatusQueryHandler(repository, NoCompliance(), Clock);

        var status = (await handler.Handle(new GetHosStatusQuery(TestDrivers.TenantId, DriverId), CancellationToken.None)).Value;

        Assert.True(status.RecordsOnFile);
        Assert.True(status.Evaluation.CanDriveNow);
        Assert.Equal(HosBand.Ontime, status.Evaluation.Band);
        Assert.Equal(13 * 3600 - (8 * 3600 + 30 * 60), status.Evaluation.RemainingDrivingTodaySeconds);
    }

    // ---- GET /api/drivers/hos/status (roster batch) ----

    [Fact]
    public async Task The_roster_batch_covers_every_driver_including_those_with_no_ledger()
    {
        var other = Guid.NewGuid();
        var repository = new FakeDutyDayRepository();
        repository.Days.Add(DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("06:30", DutyStatus.Driving)));
        var handler = new GetHosRosterStatusQueryHandler(
            repository, NoCompliance(), new FakeDriverReadService(Roster(DriverId), Roster(other, "A. Bear")), Clock);

        var result = await handler.Handle(new GetHosRosterStatusQuery(TestDrivers.TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var mine = Assert.Single(result.Value, r => r.DriverId == DriverId);
        Assert.Equal(HosWire.Driving, mine.CurrentDuty);
        Assert.False(mine.CanDriveNow);
        Assert.Equal(HosBand.Over, mine.Band);

        var theirs = Assert.Single(result.Value, r => r.DriverId == other);
        Assert.Equal(HosBand.Off, theirs.Band);
        Assert.Equal(0, theirs.TightestRemainingSeconds);
        Assert.Contains(BlockingReason.NoLedger, theirs.BlockingReasons);
        Assert.Equal(HosWire.OffDuty, theirs.CurrentDuty);
    }

    // ---- GET hos/days ----

    [Fact]
    public async Task Day_summaries_cover_every_requested_date_and_carry_the_engines_totals()
    {
        var repository = new FakeDutyDayRepository();
        var closer = Guid.NewGuid();
        repository.Days.Add(DayWith(
            Draft("06:00", DutyStatus.OnDuty),
            Draft("06:30", DutyStatus.Driving),
            Draft("12:00", DutyStatus.OffDuty, eventId: closer)));
        var handler = new GetDutyDaysQueryHandler(repository, NoCompliance(), Clock);

        var result = await handler.Handle(new GetDutyDaysQuery(TestDrivers.TenantId, DriverId, D.AddDays(-2), D.AddDays(3)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Capped at today: D-2, D-1, D.
        Assert.Equal([D.AddDays(-2), D.AddDays(-1), D], result.Value.Select(s => s.Date));

        var yesterday = result.Value[1];
        Assert.False(yesterday.HasRecords);
        Assert.Null(yesterday.DutyDayId);
        Assert.Empty(yesterday.Events);
        Assert.Equal(HosDisplay.PlanningLabel, yesterday.Label);

        var today = result.Value[2];
        Assert.True(today.HasRecords);
        Assert.Equal(5 * 3600 + 30 * 60, today.Totals.DrivingSeconds);
        Assert.Equal(6 * 3600, today.Totals.OnDutySeconds);
        Assert.Equal(3, today.Events.Count);
        Assert.Equal([HosWire.OnDuty, HosWire.Driving, HosWire.OffDuty], today.Segments.Select(s => s.Status));
        Assert.Equal(At("12:00"), today.Segments[1].ToUtc);
        Assert.Equal(Now, today.Segments[2].ToUtc);
        Assert.Equal(HosWire.DriverAppSource, today.Events[0].Source);
        Assert.False(today.Certified);
        Assert.Empty(today.Exceptions);
        Assert.Null(today.OutOfService);
    }

    [Fact]
    public async Task A_single_day_after_today_is_not_found_and_a_bad_range_is_refused()
    {
        var repository = new FakeDutyDayRepository();
        var one = new GetDutyDayQueryHandler(repository, NoCompliance(), Clock);
        var range = new GetDutyDaysQueryHandler(repository, NoCompliance(), Clock);

        Assert.Equal(HosErrors.DayNotFound, (await one.Handle(new GetDutyDayQuery(TestDrivers.TenantId, DriverId, D.AddDays(1)), CancellationToken.None)).Error);
        Assert.Equal(GetDutyDaysQueryHandler.RangeInvalid, (await range.Handle(new GetDutyDaysQuery(TestDrivers.TenantId, DriverId, D, D.AddDays(-1)), CancellationToken.None)).Error);
        Assert.Equal(GetDutyDaysQueryHandler.RangeInvalid, (await range.Handle(new GetDutyDaysQuery(TestDrivers.TenantId, DriverId, D.AddDays(-100), D), CancellationToken.None)).Error);

        var today = await one.Handle(new GetDutyDayQuery(TestDrivers.TenantId, DriverId, D), CancellationToken.None);
        Assert.True(today.IsSuccess);
        Assert.False(today.Value.HasRecords);
        Assert.Equal(D, today.Value.Date);
    }

    [Fact]
    public async Task A_past_day_is_evaluated_as_a_complete_day()
    {
        var repository = new FakeDutyDayRepository();
        var day = DutyDay.Open(TestDrivers.TenantId, DriverId, D.AddDays(-20), Now);
        Assert.True(day.Append(Draft("06:00", DutyStatus.OnDuty, dayOffset: -20), Now).IsSuccess);
        Assert.True(day.Append(Draft("06:30", DutyStatus.Driving, dayOffset: -20), Now).IsSuccess);
        Assert.True(day.Append(Draft("20:00", DutyStatus.OffDuty, dayOffset: -20), Now).IsSuccess);
        repository.Days.Add(day);
        var handler = new GetDutyDayQueryHandler(repository, NoCompliance(), Clock);

        var summary = (await handler.Handle(new GetDutyDayQuery(TestDrivers.TenantId, DriverId, D.AddDays(-20)), CancellationToken.None)).Value;

        Assert.True(summary.HasRecords);
        Assert.True(summary.Totals.Complete);
        Assert.Equal(13 * 3600 + 30 * 60, summary.Totals.DrivingSeconds);
        Assert.Contains(summary.Violations, v => v.Rule == HosRule.DailyDriving13h);
        Assert.All(summary.Violations, v => Assert.Equal(D.AddDays(-20), v.Date));
        Assert.Equal(At("00:00", -19), summary.Segments[^1].ToUtc);
    }
}
