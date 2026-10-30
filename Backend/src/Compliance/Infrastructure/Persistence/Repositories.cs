using Microsoft.EntityFrameworkCore;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Domain.RuleSets;
using Npgsql;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>
/// Write-side repository over <see cref="ComplianceDbContext"/> (tenant-filtered) and the
/// module's unit of work. <see cref="TrySaveChangesAsync"/> turns the races the registrar
/// expects (stale series version, concurrent series/settings insert, duplicate source) into
/// outcomes rather than exceptions, clearing the whole tracker on either — the
/// audit pipeline staged snapshot/journal/outbox rows for the write too, and left tracked they
/// would commit an audit trail for a write that never happened (the Vendor repository's rule).
/// </summary>
internal sealed class ComplianceRecordRepository(ComplianceDbContext context) : IComplianceRecordRepository, IDiscardableUnitOfWork
{
    public Task<ComplianceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ComplianceRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<ComplianceRecord?> GetBySourceAsync(string sourceModule, string sourceRecordId, CancellationToken cancellationToken = default) =>
        context.ComplianceRecords.FirstOrDefaultAsync(
            r => r.SourceModule == sourceModule && r.SourceRecordId == sourceRecordId, cancellationToken);

    public async Task<IReadOnlyList<ComplianceRecord>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await context.ComplianceRecords.Where(r => ids.Contains(r.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ComplianceRecord>> ListRetainedAsync(CancellationToken cancellationToken = default) =>
        await context.ComplianceRecords
            .Where(r => r.Status == ComplianceRecordStatus.Active || r.Status == ComplianceRecordStatus.Superseded)
            .ToListAsync(cancellationToken);

    public void Add(ComplianceRecord record) => context.ComplianceRecords.Add(record);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<ComplianceSaveOutcome> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ComplianceSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            // A DocumentSeries version token was stale: another registration took the number first.
            context.ChangeTracker.Clear();
            return ComplianceSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: DocumentSeriesConfiguration.SeriesYearIndex or TenantComplianceSettingsConfiguration.TenantIndex,
        })
        {
            // The same race one step earlier: two registrations both found no (series, year) row —
            // the first of a year, or an unseeded tenant's first record — and both inserted one (or
            // both inserted default settings). The loser retries and loads the winner's row.
            context.ChangeTracker.Clear();
            return ComplianceSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ComplianceRecordConfiguration.SourceIndex,
        })
        {
            context.ChangeTracker.Clear();
            return ComplianceSaveOutcome.DuplicateSource;
        }
    }

    public Task DiscardAsync(CancellationToken cancellationToken = default)
    {
        context.ChangeTracker.Clear();
        return Task.CompletedTask;
    }
}

internal sealed class DocumentSeriesRepository(ComplianceDbContext context) : IDocumentSeriesRepository
{
    public Task<DocumentSeries?> GetAsync(DocumentSeriesCode series, int year, CancellationToken cancellationToken = default) =>
        context.DocumentSeries.FirstOrDefaultAsync(s => s.Series == series && s.Year == year, cancellationToken);

    public async Task<IReadOnlyList<DocumentSeries>> ListAsync(int year, CancellationToken cancellationToken = default) =>
        await context.DocumentSeries.Where(s => s.Year == year).OrderBy(s => s.Series).ToListAsync(cancellationToken);

    public void Add(DocumentSeries series) => context.DocumentSeries.Add(series);
}

internal sealed class TenantComplianceSettingsRepository(ComplianceDbContext context) : ITenantComplianceSettingsRepository
{
    public Task<TenantComplianceSettings?> GetAsync(CancellationToken cancellationToken = default) =>
        context.TenantComplianceSettings.FirstOrDefaultAsync(cancellationToken);

    public void Add(TenantComplianceSettings settings) => context.TenantComplianceSettings.Add(settings);
}

internal sealed class DestructionLogRepository(ComplianceDbContext context) : IDestructionLogRepository
{
    public void Add(DestructionLogEntry entry) => context.DestructionLog.Add(entry);

    public async Task<IReadOnlyList<DestructionLogEntry>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = context.DestructionLog.AsNoTracking();
        if (from is { } f)
        {
            var fromUtc = new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(d => d.DisposedAtUtc >= fromUtc);
        }

        if (to is { } t)
        {
            var toUtc = new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(d => d.DisposedAtUtc < toUtc);
        }

        return await query.OrderByDescending(d => d.DisposedAtUtc).ToListAsync(cancellationToken);
    }
}

internal sealed class TenantRuleSetApplicationRepository(ComplianceDbContext context) : ITenantRuleSetApplicationRepository
{
    public Task<TenantRuleSetApplication?> LatestAsOfAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default) =>
        context.TenantRuleSetApplications
            .Where(a => a.AppliedAtUtc <= asOfUtc)
            .OrderByDescending(a => a.AppliedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(TenantRuleSetApplication application) => context.TenantRuleSetApplications.Add(application);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);
}

/// <summary>Reference rows: reads need no session; the write runs under <see cref="SystemSession"/>.</summary>
internal sealed class RuleSetRepository(ComplianceDbContext context) : IRuleSetRepository
{
    public Task<RuleSet?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RuleSets.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<RuleSet?> FindAsync(string jurisdiction, int version, CancellationToken cancellationToken = default) =>
        context.RuleSets.AsNoTracking().FirstOrDefaultAsync(r => r.Jurisdiction == jurisdiction && r.Version == version, cancellationToken);

    public Task<RuleSet?> LatestAsync(string jurisdiction, CancellationToken cancellationToken = default) =>
        context.RuleSets.AsNoTracking()
            .Where(r => r.Jurisdiction == jurisdiction)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RuleSet>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.RuleSets.AsNoTracking().OrderBy(r => r.Jurisdiction).ThenBy(r => r.Version).ToListAsync(cancellationToken);

    public Task AddAsync(RuleSet ruleSet, CancellationToken cancellationToken = default) =>
        SystemSession.RunAsync(context, async () =>
        {
            context.RuleSets.Add(ruleSet);
            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
}

/// <summary>Reference rows: reads need no session; the upsert runs under <see cref="SystemSession"/>.</summary>
internal sealed class RegulatoryBasisRepository(ComplianceDbContext context) : IRegulatoryBasisRepository
{
    public async Task<IReadOnlyList<RegulatoryBasis>> GetByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        await context.RegulatoryBasis.AsNoTracking().Where(b => keys.Contains(b.Key)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RegulatoryBasis>> ListAsync(RegulatoryInstrument? instrument, CancellationToken cancellationToken = default)
    {
        var query = context.RegulatoryBasis.AsNoTracking();
        if (instrument is { } i)
        {
            query = query.Where(b => b.Instrument == i);
        }

        return await query.OrderBy(b => b.Instrument).ThenBy(b => b.Key).ToListAsync(cancellationToken);
    }

    public async Task<int> UpsertAsync(IReadOnlyList<RegulatoryBasisDefinition> definitions, CancellationToken cancellationToken = default)
    {
        var inserted = 0;
        await SystemSession.RunAsync(context, async () =>
        {
            var keys = definitions.Select(d => d.Key).ToList();
            var existing = await context.RegulatoryBasis.Where(b => keys.Contains(b.Key)).ToDictionaryAsync(b => b.Key, cancellationToken);

            foreach (var definition in definitions)
            {
                if (existing.TryGetValue(definition.Key, out var row))
                {
                    row.Overwrite(definition);
                }
                else
                {
                    context.RegulatoryBasis.Add(RegulatoryBasis.Create(definition));
                    inserted++;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return inserted;
    }
}

/// <summary>The retention worker's cross-tenant enumeration — a system session, like the workers'.</summary>
internal sealed class RetentionSweepSource(ComplianceDbContext context) : IRetentionSweepSource
{
    public async Task<IReadOnlyList<Guid>> ListTenantsWithRetainedRecordsAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> tenants = [];
        await SystemSession.RunAsync(context, async () =>
        {
            tenants = await context.ComplianceRecords
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Status == ComplianceRecordStatus.Active || r.Status == ComplianceRecordStatus.Superseded)
                .Select(r => r.TenantId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }, cancellationToken);
        return tenants;
    }
}
