using System.Globalization;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Alerts;

/// <summary>
/// Builds <see cref="HosAlertsResponse"/> from the same loads as the roster batch
/// (<c>GetHosRosterStatusQueryHandler</c>): every day in the lookback window, every driver's
/// earliest day, and the tenant's compliance records, assembled per driver through
/// <see cref="DutyLedgerAssembler"/> so the bands here are the bands the roster shows.
/// Deactivated drivers are left out — a departed driver's uncertified days would otherwise
/// nag forever — while Inactive (on leave) drivers stay in, since their declarations and
/// records still matter.
/// </summary>
public sealed class GetHosAlertsQueryHandler(
    IDutyDayRepository repository,
    IComplianceRecordsReader compliance,
    IDriverReadService drivers,
    TimeProvider clock)
    : IQueryHandler<GetHosAlertsQuery, HosAlertsResponse>
{
    public async Task<Result<HosAlertsResponse>> Handle(GetHosAlertsQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var nowUnix = now.ToUnixTimeSeconds();
        var today = DutyLedgerAssembler.Calendar.DayOf(nowUnix);
        var yesterday = today.AddDays(-1);
        var windowFrom = today.AddDays(-DutyLedgerAssembler.LookbackDays);

        var roster = await drivers.GetDriversAsync(cancellationToken);
        var days = await repository.LoadRangeForAllDriversAsync(windowFrom, today, cancellationToken);
        var knownFrom = await repository.EarliestLocalDatesAsync(cancellationToken);
        var records = await compliance.LoadForTenantAsync(
            DateTimeOffset.FromUnixTimeSeconds(DutyLedgerAssembler.Calendar.DayStartUnix(windowFrom)), cancellationToken);

        var daysByDriver = days.GroupBy(d => d.DriverId).ToDictionary(g => g.Key, g => g.ToList());
        var items = new List<HosAlertItemResponse>();

        foreach (var driver in roster.Where(d => d.Status != nameof(DriverStatus.Deactivated)))
        {
            var driverDays = daysByDriver.GetValueOrDefault(driver.Id) ?? [];
            var driverRecords = records.GetValueOrDefault(driver.Id) ?? ComplianceRecords.None;

            var ledger = DutyLedgerAssembler.Build(
                driver.Id,
                driverDays,
                knownFrom.TryGetValue(driver.Id, out var first) ? first : null,
                nowUnix,
                driverRecords);
            var evaluation = DutyLedgerAssembler.Evaluate(ledger, nowUnix);

            items.AddRange(AlertsFor(driver, evaluation, driverDays, driverRecords, now, yesterday));
        }

        var ordered = items
            .OrderBy(i => Enum.Parse<HosAlertKind>(i.Kind))
            .ThenBy(i => i.DriverName, StringComparer.Ordinal)
            .ToList();

        return Result.Success(new HosAlertsResponse(
            now,
            DriversNearLimit: ordered.Count(i => i.Kind == nameof(HosAlertKind.NearLimit)),
            DriversOverLimit: ordered.Count(i => i.Kind == nameof(HosAlertKind.OverLimit)),
            OutOfService: ordered.Count(i => i.Kind == nameof(HosAlertKind.OutOfService)),
            OpenMalfunctions: ordered.Count(i => i.Kind == nameof(HosAlertKind.EldMalfunction)),
            UncertifiedYesterday: ordered.Count(i => i.Kind == nameof(HosAlertKind.Uncertified)),
            UnassignedExceptions: ordered.Count(i => i.Kind == nameof(HosAlertKind.ExceptionDeclared)),
            ordered,
            HosDisplay.PlanningLabel));
    }

    /// <summary>One driver's alerts — public so the shape is testable without the loads.</summary>
    public static IEnumerable<HosAlertItemResponse> AlertsFor(
        DriverResponse driver,
        HosEvaluation evaluation,
        IReadOnlyList<DutyDay> days,
        ComplianceRecords records,
        DateTimeOffset now,
        DateOnly yesterday)
    {
        var name = driver.Name;

        // An active declaration first: it is the one thing that stops the driver outright.
        var outOfService = records.OutOfService
            .Where(o => o.IsActiveAt(now))
            .OrderBy(o => o.DeclaredAtUtc)
            .FirstOrDefault();
        if (outOfService is not null)
        {
            var until = outOfService.EffectiveUntilUtc is { } end
                ? $"until {HosLocalTime.DateAndClock(end)}"
                : "until lifted";
            yield return Item(driver, HosAlertKind.OutOfService,
                $"{name} is out of service {until} — {outOfService.Reason}.");
        }

        switch (evaluation.Band)
        {
            case HosBand.Over:
                yield return Item(driver, HosAlertKind.OverLimit,
                    $"{name} is over an hours-of-service limit ({Describe(evaluation.BlockingReasons)}) and must not drive.");
                break;
            case HosBand.Soon:
                yield return Item(driver, HosAlertKind.NearLimit,
                    $"{name} has {Duration(evaluation.TightestRemainingSeconds)} left before the tightest limit.");
                break;
        }

        foreach (var malfunction in records.Malfunctions.Where(m => m.IsOpen).OrderBy(m => m.NoticedAtUtc))
        {
            yield return Item(driver, HosAlertKind.EldMalfunction,
                $"{name}: ELD malfunction {malfunction.Code} reported {HosLocalTime.DateAndClock(malfunction.NoticedAtUtc)}; paper logs until resolved.");
        }

        foreach (var exception in records.Exceptions
                     .Where(e => e.Status == HosExceptionStatus.Declared)
                     .OrderBy(e => e.EffectiveFromUtc))
        {
            yield return Item(driver, HosAlertKind.ExceptionDeclared,
                $"{name} declared {HosDisplay.ExceptionKindToWire(exception.Kind)} at {HosLocalTime.DateAndClock(exception.EffectiveFromUtc)} — awaiting acknowledgement: {exception.Reason}.");
        }

        // A day exists only once an event was recorded, so "on file and not certified" is the
        // whole test — an empty yesterday has nothing to sign.
        var uncertified = days.FirstOrDefault(d => d.LocalDate == yesterday && !d.IsCertified);
        if (uncertified is not null)
        {
            yield return Item(driver, HosAlertKind.Uncertified,
                $"{name} has not certified yesterday's record ({yesterday:yyyy-MM-dd}).");
        }
    }

    private static HosAlertItemResponse Item(DriverResponse driver, HosAlertKind kind, string message) =>
        new(driver.Id, driver.Name, kind.ToString(), message);

    /// <summary>"2h 05m" / "45m" from a figure in seconds; never negative.</summary>
    public static string Duration(int seconds)
    {
        var minutes = Math.Max(0, seconds) / 60;
        return minutes >= 60
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes / 60}h {minutes % 60:00}m")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}m");
    }

    /// <summary>The engine's blocking reasons as a dispatcher reads them, in the engine's order.</summary>
    public static string Describe(IReadOnlyList<BlockingReason> reasons) =>
        reasons.Count == 0
            ? "limit reached"
            : string.Join(", ", reasons.Select(reason => reason switch
            {
                BlockingReason.NoLedger => "no ledger on file",
                BlockingReason.RecordsIncomplete => "records incomplete",
                BlockingReason.OutOfService => "out of service",
                BlockingReason.Rest24Expired => "24h off-duty overdue",
                BlockingReason.CycleExhausted => "70h cycle used",
                BlockingReason.DailyDrivingExhausted => "13h daily driving used",
                BlockingReason.DailyOnDutyExhausted => "14h daily on-duty used",
                BlockingReason.ShiftDrivingExhausted => "13h shift driving used",
                BlockingReason.ShiftOnDutyExhausted => "14h shift on-duty used",
                BlockingReason.ShiftWindowClosed => "16h shift window closed",
                _ => reason.ToString(),
            }));
}
