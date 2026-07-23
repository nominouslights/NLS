using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;

namespace NorthernLink.Drivers.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="HosLogEntry"/> into <c>drivers.rm_hos_log_entries</c> — and, because
/// rm_drivers denormalizes the driver's latest-duty rollup (<c>latest_duty_status</c> /
/// <c>latest_driving_hours</c> / <c>latest_hos_date</c>), refreshes the parent driver's read
/// row in the same batch via <see cref="HosStats"/>. This mirrors how
/// <see cref="DriverCredentialProjection"/> fans out the credential stats. HOS entries are
/// insert-only, but the delete arm is kept for symmetry with the other projections.
/// </summary>
internal sealed class HosLogEntryProjection : IProjection<DriversDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(HosLogEntry));

    public async Task ApplyAsync(DriversDbContext context, Guid entryId, CancellationToken cancellationToken)
    {
        var source = await context.HosLogEntries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        var row = await context.HosLogEntryReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == entryId, cancellationToken);

        // On delete the source is gone, so the read row is the only place the parent driver
        // id is still known — capture it before removing.
        var driverId = source?.DriverId ?? row?.DriverId;

        if (source is null)
        {
            if (row is not null)
            {
                context.HosLogEntryReadModels.Remove(row);
            }
        }
        else
        {
            if (row is null)
            {
                row = new HosLogEntryReadModel();
                context.HosLogEntryReadModels.Add(row);
            }

            Map(source, row);
        }

        if (driverId is { } id)
        {
            await RefreshDriverStatsAsync(context, id, cancellationToken);
        }
    }

    public async Task RebuildAllAsync(DriversDbContext context, CancellationToken cancellationToken)
    {
        var sources = await context.HosLogEntries.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.HosLogEntryReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var source in sources)
        {
            seen.Add(source.Id);

            if (!rowsById.TryGetValue(source.Id, out var row))
            {
                row = new HosLogEntryReadModel();
                context.HosLogEntryReadModels.Add(row);
            }

            Map(source, row);
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.HosLogEntryReadModels.Remove(row);
            }
        }

        // rm_drivers HOS rollup is rebuilt by DriverProjection.RebuildAllAsync from the same
        // write tables, so no extra pass is needed here.
    }

    /// <summary>Recomputes the parent driver's denormalized HOS rollup from the write side.</summary>
    private static async Task RefreshDriverStatsAsync(
        DriversDbContext context,
        Guid driverId,
        CancellationToken cancellationToken)
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

        var entries = await context.HosLogEntries
            .IgnoreQueryFilters()
            .Where(e => e.DriverId == driverId)
            .ToListAsync(cancellationToken);

        HosStats.Apply(driverRow, entries);
    }

    private static void Map(HosLogEntry source, HosLogEntryReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.DriverId = source.DriverId;
        row.Date = source.Date;
        row.Duty = source.Duty;
        row.OnDutyHours = source.OnDutyHours;
        row.DrivingHours = source.DrivingHours;
        row.OffDutyHours = source.OffDutyHours;
        row.Source = source.Source;
        row.EnteredBy = source.EnteredBy;
        row.Note = source.Note;
        row.RecordedAtUtc = source.RecordedAtUtc;
        row.Version = source.Version;
    }
}

/// <summary>
/// The single implementation of the rm_drivers denormalized HOS rollup fields, shared by
/// <see cref="DriverProjection"/> and <see cref="HosLogEntryProjection"/> so the two writers
/// can never disagree — mirrors <c>DriverCredentialStats</c>. The rollup reflects the
/// driver's newest entry (latest <c>Date</c>, tie-broken by <c>RecordedAtUtc</c>); duty is
/// stored as the friendly string the roster's duty chip keys on.
/// </summary>
internal static class HosStats
{
    public static void Apply(DriverReadModel row, IReadOnlyCollection<HosLogEntry> entries)
    {
        var latest = entries
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.RecordedAtUtc)
            .FirstOrDefault();

        row.LatestDutyStatus = latest is null ? null : HosDisplay.DutyToWire(latest.Duty);
        row.LatestDrivingHours = latest?.DrivingHours;
        row.LatestHosDate = latest?.Date;
    }
}
