using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Status;

public sealed class GetHosRosterStatusQueryHandler(
    IDutyDayRepository repository,
    IComplianceRecordsReader compliance,
    IDriverReadService drivers,
    TimeProvider clock)
    : IQueryHandler<GetHosRosterStatusQuery, IReadOnlyList<HosRosterStatusResponse>>
{
    public async Task<Result<IReadOnlyList<HosRosterStatusResponse>>> Handle(GetHosRosterStatusQuery query, CancellationToken cancellationToken)
    {
        var nowUnix = clock.GetUtcNow().ToUnixTimeSeconds();
        var today = DutyLedgerAssembler.Calendar.DayOf(nowUnix);
        var windowFrom = today.AddDays(-DutyLedgerAssembler.LookbackDays);

        var roster = await drivers.GetDriversAsync(cancellationToken);
        var days = await repository.LoadRangeForAllDriversAsync(windowFrom, today, cancellationToken);
        var knownFrom = await repository.EarliestLocalDatesAsync(cancellationToken);
        var records = await compliance.LoadForTenantAsync(
            DateTimeOffset.FromUnixTimeSeconds(DutyLedgerAssembler.Calendar.DayStartUnix(windowFrom)), cancellationToken);

        var daysByDriver = days.GroupBy(d => d.DriverId).ToDictionary(g => g.Key, g => g.ToList());
        var rows = new List<HosRosterStatusResponse>(roster.Count);

        foreach (var driver in roster)
        {
            var ledger = DutyLedgerAssembler.Build(
                driver.Id,
                daysByDriver.GetValueOrDefault(driver.Id) ?? [],
                knownFrom.TryGetValue(driver.Id, out var first) ? first : null,
                nowUnix,
                records.GetValueOrDefault(driver.Id) ?? ComplianceRecords.None);
            var evaluation = DutyLedgerAssembler.Evaluate(ledger, nowUnix);

            rows.Add(new HosRosterStatusResponse(
                driver.Id,
                evaluation.Band,
                evaluation.TightestRemainingSeconds,
                evaluation.CanDriveNow,
                evaluation.BlockingReasons,
                HosWire.Status(evaluation.CurrentStatus)));
        }

        return Result.Success<IReadOnlyList<HosRosterStatusResponse>>(rows);
    }
}
