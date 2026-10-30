using NorthernLink.Drivers.Application.Hos.Alerts;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using Xunit;
using DutyStatus = NorthernLink.Drivers.Domain.Hos.DutyStatus;
using HosException = NorthernLink.Drivers.Domain.Hos.HosException;
using static NorthernLink.Drivers.Tests.Hos.HosTestSupport;

namespace NorthernLink.Drivers.Tests.Hos;

/// <summary>
/// <c>GET /api/drivers/hos/alerts/today</c>: each alert kind appears exactly when its fact
/// holds, the counts summarise the rows, and the list is ordered most urgent first. Same
/// fixtures as the roster batch — D = 2026-09-14, on duty 06:00, driving from 06:30.
/// </summary>
public class HosAlertsQueryHandlerTests
{
    private static readonly Guid OfficerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TripId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static FakeDutyDayRepository TodayDriving()
    {
        var repository = new FakeDutyDayRepository();
        repository.Days.Add(DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("06:30", DutyStatus.Driving)));
        return repository;
    }

    /// <summary>Twenty certified days off before today, so the records are complete and only today's hours count.</summary>
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

    private static GetHosAlertsQueryHandler Handler(
        FakeDutyDayRepository days, FakeComplianceRecords records, DateTimeOffset? now = null, string status = "Active") =>
        new(days, records, new FakeDriverReadService(Roster(DriverId) with { Status = status }), new FixedClock(now ?? Now));

    private static async Task<HosAlertsResponse> Alerts(GetHosAlertsQueryHandler handler) =>
        (await handler.Handle(new GetHosAlertsQuery(TestDrivers.TenantId), CancellationToken.None)).Value;

    private static IEnumerable<HosAlertItemResponse> OfKind(HosAlertsResponse alerts, HosAlertKind kind) =>
        alerts.Items.Where(i => i.Kind == kind.ToString());

    [Fact]
    public async Task A_driver_well_inside_every_limit_raises_nothing()
    {
        // 8.5h driving by 15:00 with complete records: nothing to flag.
        var alerts = await Alerts(Handler(CompleteRecordsThenDriving(), new FakeComplianceRecords()));

        Assert.Empty(alerts.Items);
        Assert.Equal(0, alerts.DriversNearLimit + alerts.DriversOverLimit + alerts.OutOfService
            + alerts.OpenMalfunctions + alerts.UncertifiedYesterday + alerts.UnassignedExceptions);
        Assert.Equal(Now, alerts.AsOfUtc);
        Assert.Contains("RODS", alerts.Label);
    }

    [Fact]
    public async Task Inside_the_soon_band_the_driver_is_near_a_limit_with_the_remaining_time()
    {
        // Driving since 06:30; at 18:00 that is 11.5h → 1h 30m of the 13h daily driving left.
        var alerts = await Alerts(Handler(CompleteRecordsThenDriving(), new FakeComplianceRecords(), At("18:00")));

        var item = Assert.Single(OfKind(alerts, HosAlertKind.NearLimit));
        Assert.Equal(DriverId, item.DriverId);
        Assert.Equal("J. Spence", item.DriverName);
        Assert.Equal("J. Spence has 1h 30m left before the tightest limit.", item.Message);
        Assert.Equal(1, alerts.DriversNearLimit);
        Assert.Equal(0, alerts.DriversOverLimit);
    }

    [Fact]
    public async Task Past_a_limit_the_driver_is_over_and_the_reasons_are_named()
    {
        // 13.5h of driving by 20:00.
        var alerts = await Alerts(Handler(CompleteRecordsThenDriving(), new FakeComplianceRecords(), At("20:00")));

        var item = Assert.Single(OfKind(alerts, HosAlertKind.OverLimit));
        Assert.StartsWith("J. Spence is over an hours-of-service limit (", item.Message);
        Assert.Contains("13h daily driving used", item.Message);
        Assert.EndsWith(") and must not drive.", item.Message);
        Assert.Equal(1, alerts.DriversOverLimit);
        Assert.Empty(OfKind(alerts, HosAlertKind.NearLimit));
    }

    [Fact]
    public async Task An_active_out_of_service_declaration_is_listed_with_its_end_and_reason()
    {
        var records = new FakeComplianceRecords();
        var r = DriverOutOfService.Record(TestDrivers.TenantId, DriverId, At("14:00"), OfficerId, Now, null, false, "Inspection", null).Value;
        r.ClearDomainEvents();
        records.OutOfService.Add(r);

        var alerts = await Alerts(Handler(CompleteRecordsThenDriving(), records));

        var item = Assert.Single(OfKind(alerts, HosAlertKind.OutOfService));
        Assert.Equal("J. Spence is out of service until Sep 17, 14:00 — Inspection.", item.Message);
        Assert.Equal(1, alerts.OutOfService);
    }

    [Fact]
    public async Task An_open_ended_declaration_says_until_lifted_and_a_lifted_one_is_gone()
    {
        var records = new FakeComplianceRecords();
        var open = DriverOutOfService.Record(TestDrivers.TenantId, DriverId, At("14:00"), OfficerId, Now, null, true, "Inspection", null).Value;
        records.OutOfService.Add(open);

        var listed = await Alerts(Handler(CompleteRecordsThenDriving(), records));
        Assert.Equal("J. Spence is out of service until lifted — Inspection.", Assert.Single(OfKind(listed, HosAlertKind.OutOfService)).Message);

        Assert.True(open.Lift(OfficerId, At("14:30"), "cleared").IsSuccess);
        var cleared = await Alerts(Handler(CompleteRecordsThenDriving(), records));
        Assert.Empty(OfKind(cleared, HosAlertKind.OutOfService));
        Assert.Equal(0, cleared.OutOfService);
    }

    [Fact]
    public async Task Each_open_malfunction_is_one_row_and_a_resolved_one_is_none()
    {
        var records = new FakeComplianceRecords();
        var open = EldMalfunctionReport.Report(TestDrivers.TenantId, DriverId, "J. Spence", null, "P", At("07:10"), UserId, Now, "No GPS fix").Value;
        var resolved = EldMalfunctionReport.Report(TestDrivers.TenantId, DriverId, "J. Spence", null, "E", At("08:00"), UserId, Now, "Engine sync").Value;
        Assert.True(resolved.Resolve(OfficerId, At("09:00"), null).IsSuccess);
        records.Malfunctions.AddRange([open, resolved]);

        var alerts = await Alerts(Handler(CompleteRecordsThenDriving(), records));

        var item = Assert.Single(OfKind(alerts, HosAlertKind.EldMalfunction));
        Assert.Equal("J. Spence: ELD malfunction P reported Sep 14, 07:10; paper logs until resolved.", item.Message);
        Assert.Equal(1, alerts.OpenMalfunctions);
    }

    [Fact]
    public async Task A_declared_exception_awaits_acknowledgement_and_an_acknowledged_one_does_not()
    {
        var records = new FakeComplianceRecords();
        var declared = HosException.Declare(
            TestDrivers.TenantId, DriverId, HosExceptionKind.AdverseDrivingConditions, UserId, Now,
            At("10:00"), null, "Whiteout", null, true, TimeSpan.FromMinutes(90), false, TripId).Value;
        var acknowledged = HosException.Declare(
            TestDrivers.TenantId, DriverId, HosExceptionKind.Emergency, UserId, Now,
            At("11:00"), At("11:30"), "Medical", null, false, null, false, TripId).Value;
        Assert.True(acknowledged.Acknowledge(OfficerId, At("12:00")).IsSuccess);
        records.Exceptions.AddRange([declared, acknowledged]);

        var alerts = await Alerts(Handler(CompleteRecordsThenDriving(), records));

        var item = Assert.Single(OfKind(alerts, HosAlertKind.ExceptionDeclared));
        Assert.Equal(
            $"J. Spence declared {HosWire.AdverseDrivingConditionsKind} at Sep 14, 10:00 — awaiting acknowledgement: Whiteout.",
            item.Message);
        Assert.Equal(1, alerts.UnassignedExceptions);
    }

    [Fact]
    public async Task Yesterdays_record_on_file_and_uncertified_is_flagged_a_certified_or_absent_one_is_not()
    {
        // Uncertified yesterday: on file, unsigned.
        var uncertified = TodayDriving();
        var yesterday = DutyDay.Open(TestDrivers.TenantId, DriverId, D.AddDays(-1), Now);
        Assert.True(yesterday.Append(Draft("08:00", DutyStatus.OnDuty, dayOffset: -1), Now).IsSuccess);
        uncertified.Days.Add(yesterday);

        var flagged = await Alerts(Handler(uncertified, new FakeComplianceRecords()));
        var item = Assert.Single(OfKind(flagged, HosAlertKind.Uncertified));
        Assert.Equal("J. Spence has not certified yesterday's record (2026-09-13).", item.Message);
        Assert.Equal(1, flagged.UncertifiedYesterday);

        // Certified: every prior day in CompleteRecordsThenDriving is signed.
        var certified = await Alerts(Handler(CompleteRecordsThenDriving(), new FakeComplianceRecords()));
        Assert.Empty(OfKind(certified, HosAlertKind.Uncertified));

        // Absent: nothing on file for yesterday, nothing to sign.
        var absent = await Alerts(Handler(TodayDriving(), new FakeComplianceRecords()));
        Assert.Empty(OfKind(absent, HosAlertKind.Uncertified));
    }

    [Fact]
    public async Task A_deactivated_driver_raises_nothing_an_inactive_one_still_does()
    {
        var records = new FakeComplianceRecords();
        records.Malfunctions.Add(EldMalfunctionReport.Report(TestDrivers.TenantId, DriverId, "J. Spence", null, "P", At("07:10"), UserId, Now, "No GPS fix").Value);

        var deactivated = await Alerts(Handler(CompleteRecordsThenDriving(), records, status: "Deactivated"));
        Assert.Empty(deactivated.Items);

        var inactive = await Alerts(Handler(CompleteRecordsThenDriving(), records, status: "Inactive"));
        Assert.Single(OfKind(inactive, HosAlertKind.EldMalfunction));
    }

    [Fact]
    public async Task Items_are_ordered_most_urgent_first_and_the_counts_match_the_rows()
    {
        var records = new FakeComplianceRecords();
        records.OutOfService.Add(DriverOutOfService.Record(TestDrivers.TenantId, DriverId, At("14:00"), OfficerId, Now, null, false, "Inspection", null).Value);
        records.Malfunctions.Add(EldMalfunctionReport.Report(TestDrivers.TenantId, DriverId, "J. Spence", null, "P", At("07:10"), UserId, Now, "No GPS fix").Value);
        records.Exceptions.Add(HosException.Declare(
            TestDrivers.TenantId, DriverId, HosExceptionKind.AdverseDrivingConditions, UserId, Now,
            At("10:00"), null, "Whiteout", null, true, TimeSpan.FromMinutes(90), false, TripId).Value);
        var days = TodayDriving();
        var yesterday = DutyDay.Open(TestDrivers.TenantId, DriverId, D.AddDays(-1), Now);
        Assert.True(yesterday.Append(Draft("08:00", DutyStatus.OnDuty, dayOffset: -1), Now).IsSuccess);
        days.Days.Add(yesterday);

        var alerts = await Alerts(Handler(days, records));

        var kinds = alerts.Items.Select(i => Enum.Parse<HosAlertKind>(i.Kind)).ToList();
        Assert.Equal(kinds.OrderBy(k => k), kinds);
        Assert.Equal(HosAlertKind.OutOfService, kinds[0]);
        Assert.Equal(HosAlertKind.Uncertified, kinds[^1]);
        Assert.Equal(1, alerts.OutOfService);
        Assert.Equal(1, alerts.OpenMalfunctions);
        Assert.Equal(1, alerts.UnassignedExceptions);
        Assert.Equal(1, alerts.UncertifiedYesterday);
        Assert.Equal(
            alerts.DriversNearLimit + alerts.DriversOverLimit + alerts.OutOfService + alerts.OpenMalfunctions
            + alerts.UncertifiedYesterday + alerts.UnassignedExceptions,
            alerts.Items.Count);
    }

    [Theory]
    [InlineData(5400, "1h 30m")]
    [InlineData(2700, "45m")]
    [InlineData(0, "0m")]
    [InlineData(-120, "0m")]
    [InlineData(7200, "2h 00m")]
    public void Durations_read_as_hours_and_minutes(int seconds, string expected)
    {
        Assert.Equal(expected, GetHosAlertsQueryHandler.Duration(seconds));
    }

    [Fact]
    public void Blocking_reasons_read_as_prose_in_engine_order()
    {
        Assert.Equal("limit reached", GetHosAlertsQueryHandler.Describe([]));
        Assert.Equal(
            "out of service, 13h daily driving used",
            GetHosAlertsQueryHandler.Describe([BlockingReason.OutOfService, BlockingReason.DailyDrivingExhausted]));
    }
}
