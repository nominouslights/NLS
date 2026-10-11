using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.DutyDays;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;

namespace NorthernLink.Drivers.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="DutyDay"/> into <c>drivers.rm_duty_days</c> — running the rule engine
/// over the driver's surrounding days so the row carries the engine's totals and that day's
/// violation count, not a naive sum — and, because <c>rm_drivers</c> denormalizes the driver's
/// latest-duty rollup, refreshes the parent driver's read row in the same batch via
/// <see cref="HosStats"/>, exactly as <see cref="HosLogEntryProjection"/> does.
/// </summary>
internal sealed class DutyDayProjection : IProjection<DriversDbContext>
{
    private static readonly JsonSerializerOptions EventsJsonOptions = new(JsonSerializerDefaults.Web);

    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(DutyDay));

    public async Task ApplyAsync(DriversDbContext context, Guid dutyDayId, CancellationToken cancellationToken)
    {
        var source = await context.DutyDays
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dutyDayId, cancellationToken);

        var row = await context.DutyDayReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == dutyDayId, cancellationToken);

        var driverId = source?.DriverId ?? row?.DriverId;

        if (source is null)
        {
            if (row is not null)
            {
                context.DutyDayReadModels.Remove(row);
            }
        }
        else
        {
            if (row is null)
            {
                row = new DutyDayReadModel();
                context.DutyDayReadModels.Add(row);
            }

            var surrounding = await context.DutyDays
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.DriverId == source.DriverId
                    && d.LocalDate >= source.LocalDate.AddDays(-DutyLedgerAssembler.LookbackDays)
                    && d.LocalDate <= source.LocalDate)
                .ToListAsync(cancellationToken);
            var earliest = await EarliestAsync(context, source.DriverId, cancellationToken);

            Map(source, surrounding, earliest, row, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        if (driverId is { } id)
        {
            await RefreshDriverStatsAsync(context, id, cancellationToken);
        }
    }

    public async Task RebuildAllAsync(DriversDbContext context, CancellationToken cancellationToken)
    {
        var sources = await context.DutyDays.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        var rows = await context.DutyDayReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var byDriver in sources.GroupBy(d => d.DriverId))
        {
            var days = byDriver.OrderBy(d => d.LocalDate).ToList();
            var earliest = days[0].LocalDate;

            foreach (var source in days)
            {
                seen.Add(source.Id);

                if (!rowsById.TryGetValue(source.Id, out var row))
                {
                    row = new DutyDayReadModel();
                    context.DutyDayReadModels.Add(row);
                }

                var surrounding = days
                    .Where(d => d.LocalDate >= source.LocalDate.AddDays(-DutyLedgerAssembler.LookbackDays) && d.LocalDate <= source.LocalDate)
                    .ToList();
                Map(source, surrounding, earliest, row, nowUnix);
            }
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.DutyDayReadModels.Remove(row);
            }
        }

        // rm_drivers HOS rollup is rebuilt by DriverProjection.RebuildAllAsync from the same
        // write tables, so no extra pass is needed here.
    }

    private static async Task<DateOnly?> EarliestAsync(DriversDbContext context, Guid driverId, CancellationToken cancellationToken) =>
        await context.DutyDays
            .IgnoreQueryFilters()
            .Where(d => d.DriverId == driverId)
            .OrderBy(d => d.LocalDate)
            .Select(d => (DateOnly?)d.LocalDate)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Recomputes the parent driver's denormalized HOS rollup from the write side.</summary>
    private static async Task RefreshDriverStatsAsync(DriversDbContext context, Guid driverId, CancellationToken cancellationToken)
    {
        var driverRow = await context.DriverReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == driverId, cancellationToken);

        // No driver read row yet — DriverProjection computes the rollup when it materializes
        // the row (its ApplyAsync always recomputes the HOS stats).
        if (driverRow is null)
        {
            return;
        }

        var dutyDays = await context.DutyDays
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => d.DriverId == driverId)
            .ToListAsync(cancellationToken);

        var entries = await context.HosLogEntries
            .IgnoreQueryFilters()
            .Where(e => e.DriverId == driverId)
            .ToListAsync(cancellationToken);

        HosStats.Apply(driverRow, dutyDays, entries, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>
    /// The engine runs with as-of parked at the end of the day (or now, for today), so a past
    /// day is evaluated complete and its figures stop moving once it is over.
    /// </summary>
    private static void Map(DutyDay source, IReadOnlyList<DutyDay> surrounding, DateOnly? earliest, DutyDayReadModel row, long nowUnix)
    {
        var asOf = Math.Min(nowUnix, DutyLedgerAssembler.Calendar.DayEndUnix(source.LocalDate));
        var ledger = DutyLedgerAssembler.Build(source.DriverId, surrounding, earliest, asOf);
        var evaluation = DutyLedgerAssembler.Evaluate(ledger, asOf);
        var totals = evaluation.Days.FirstOrDefault(d => d.Date == source.LocalDate);
        var timeline = source.EffectiveTimeline();

        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.DriverId = source.DriverId;
        row.LocalDate = source.LocalDate;
        row.DrivingMinutes = (totals?.DrivingSeconds ?? 0) / 60;
        row.OnDutyMinutes = (totals?.OnDutySeconds ?? 0) / 60;
        row.OffDutyMinutes = (totals?.OffDutySeconds ?? 0) / 60;
        row.PersonalConveyanceKm = source.PersonalConveyanceKm;
        row.LastStatus = timeline.Count == 0 ? null : HosDisplay.DutyToWire(timeline[^1].Status);
        row.Certified = source.IsCertified;
        row.CertifiedAtUtc = source.CertifiedAtUtc;
        row.RecertificationRequired = source.RecertificationRequired;
        row.ViolationCount = evaluation.Violations.Count(v => v.Date == source.LocalDate);
        row.EventCount = timeline.Count;
        row.EventsJson = JsonSerializer.Serialize(timeline.Select(DutyDaySummaryBuilder.ToResponse).ToList(), EventsJsonOptions);
        row.Version = source.Version;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
    }
}
