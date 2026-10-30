using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>
/// Write-side repositories for the compliance aggregates over <see cref="DriversDbContext"/>
/// (tenant-filtered). All four wrap the one scoped context, so a handler that adds through two
/// of them commits both with a single save — the out-of-service declaration and the finding it
/// opens are the case that relies on it.
/// </summary>
internal sealed class HosExceptionRepository(DriversDbContext context) : IHosExceptionRepository
{
    public Task<HosException?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.HosExceptions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(HosException exception) => context.HosExceptions.Add(exception);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

internal sealed class DriverOutOfServiceRepository(DriversDbContext context) : IDriverOutOfServiceRepository
{
    public Task<DriverOutOfService?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.DriverOutOfServiceRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DriverOutOfService>> ActiveForDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        await context.DriverOutOfServiceRecords
            .Where(r => r.DriverId == driverId && r.Status == OutOfServiceStatus.Active)
            .ToListAsync(cancellationToken);

    public void Add(DriverOutOfService record) => context.DriverOutOfServiceRecords.Add(record);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

internal sealed class EldMalfunctionRepository(DriversDbContext context) : IEldMalfunctionRepository
{
    public Task<EldMalfunctionReport?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.EldMalfunctionReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void Add(EldMalfunctionReport report) => context.EldMalfunctionReports.Add(report);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

internal sealed class ComplianceFindingRepository(DriversDbContext context) : IComplianceFindingRepository
{
    public Task<ComplianceFinding?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ComplianceFindings.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public void Add(ComplianceFinding finding) => context.ComplianceFindings.Add(finding);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
