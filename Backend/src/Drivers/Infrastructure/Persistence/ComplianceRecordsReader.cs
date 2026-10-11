using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>
/// Loads the compliance records the engine and the reads need, from the write tables
/// (tenant-filtered; untracked). The window filter is "touches [fromUtc, ∞)": an exception or
/// span that started earlier but is still open counts, and so does one that ended inside the
/// window. The same query, with the tenant filter bypassed, serves the projection worker through
/// <see cref="ComplianceRecordsQueries"/>.
/// </summary>
internal sealed class ComplianceRecordsReader(DriversDbContext context) : IComplianceRecordsReader
{
    public async Task<ComplianceRecords?> LoadAsync(Guid driverId, DateTimeOffset fromUtc, CancellationToken cancellationToken = default)
    {
        var driver = await context.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        return await ComplianceRecordsQueries.LoadAsync(context, driver, fromUtc, ignoreQueryFilters: false, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ComplianceRecords>> LoadForTenantAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken = default)
    {
        var drivers = await context.Drivers.AsNoTracking().ToListAsync(cancellationToken);
        var fromDate = DutyLedgerAssembler.Calendar.DayOf(fromUtc.ToUnixTimeSeconds());

        var exceptions = await context.HosExceptions.AsNoTracking()
            .Where(e => e.EffectiveToUtc == null || e.EffectiveToUtc >= fromUtc)
            .ToListAsync(cancellationToken);
        var outOfService = await context.DriverOutOfServiceRecords.AsNoTracking()
            .Where(r => r.Status == OutOfServiceStatus.Active || r.LiftedAtUtc >= fromUtc || r.UntilUtc == null || r.UntilUtc >= fromUtc)
            .ToListAsync(cancellationToken);
        var malfunctions = await context.EldMalfunctionReports.AsNoTracking()
            .Where(m => m.ResolvedAtUtc == null || m.ResolvedAtUtc >= fromUtc)
            .ToListAsync(cancellationToken);
        var findings = await context.ComplianceFindings.AsNoTracking()
            .Where(f => f.LocalDate >= fromDate)
            .ToListAsync(cancellationToken);

        var byException = exceptions.ToLookup(e => e.DriverId);
        var byOos = outOfService.ToLookup(r => r.DriverId);
        var byMalfunction = malfunctions.ToLookup(m => m.DriverId);
        var byFinding = findings.ToLookup(f => f.DriverId);

        return drivers.ToDictionary(
            d => d.Id,
            d => ComplianceRecordsQueries.Assemble(
                d, byException[d.Id].ToList(), byOos[d.Id].ToList(), byMalfunction[d.Id].ToList(), byFinding[d.Id].ToList()));
    }
}

/// <summary>The one query behind <see cref="ComplianceRecordsReader"/> and the projection worker's engine runs.</summary>
internal static class ComplianceRecordsQueries
{
    public static async Task<ComplianceRecords> LoadAsync(
        DriversDbContext context,
        Driver driver,
        DateTimeOffset fromUtc,
        bool ignoreQueryFilters,
        CancellationToken cancellationToken)
    {
        var fromDate = DutyLedgerAssembler.Calendar.DayOf(Math.Max(fromUtc.ToUnixTimeSeconds(), DateTimeOffset.UnixEpoch.ToUnixTimeSeconds()));

        IQueryable<HosException> exceptions = context.HosExceptions.AsNoTracking();
        IQueryable<DriverOutOfService> outOfService = context.DriverOutOfServiceRecords.AsNoTracking();
        IQueryable<EldMalfunctionReport> malfunctions = context.EldMalfunctionReports.AsNoTracking();
        IQueryable<ComplianceFinding> findings = context.ComplianceFindings.AsNoTracking();

        if (ignoreQueryFilters)
        {
            exceptions = exceptions.IgnoreQueryFilters();
            outOfService = outOfService.IgnoreQueryFilters();
            malfunctions = malfunctions.IgnoreQueryFilters();
            findings = findings.IgnoreQueryFilters();
        }

        var driverId = driver.Id;
        return Assemble(
            driver,
            await exceptions
                .Where(e => e.DriverId == driverId && (e.EffectiveToUtc == null || e.EffectiveToUtc >= fromUtc))
                .OrderBy(e => e.EffectiveFromUtc)
                .ToListAsync(cancellationToken),
            await outOfService
                .Where(r => r.DriverId == driverId
                    && (r.Status == OutOfServiceStatus.Active || r.LiftedAtUtc >= fromUtc || r.UntilUtc == null || r.UntilUtc >= fromUtc))
                .OrderBy(r => r.DeclaredAtUtc)
                .ToListAsync(cancellationToken),
            await malfunctions
                .Where(m => m.DriverId == driverId && (m.ResolvedAtUtc == null || m.ResolvedAtUtc >= fromUtc))
                .OrderBy(m => m.NoticedAtUtc)
                .ToListAsync(cancellationToken),
            await findings
                .Where(f => f.DriverId == driverId && f.LocalDate >= fromDate)
                .OrderBy(f => f.LocalDate)
                .ToListAsync(cancellationToken));
    }

    public static ComplianceRecords Assemble(
        Driver driver,
        IReadOnlyList<HosException> exceptions,
        IReadOnlyList<DriverOutOfService> outOfService,
        IReadOnlyList<EldMalfunctionReport> malfunctions,
        IReadOnlyList<ComplianceFinding> findings) => new(
        exceptions,
        outOfService,
        malfunctions,
        findings,
        driver.PriorRecordsCoveredThroughDate,
        driver.PriorRecordsAttestedAtUtc,
        driver.PriorRecordsAttestedByUserId,
        driver.PriorRecordsNote,
        driver.WorksForOtherCarriers);
}
