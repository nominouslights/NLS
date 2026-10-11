using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Domain.RuleSets;

namespace NorthernLink.Compliance.Application.Abstractions;

/// <summary>Outcome of a save that may lose a race — see <see cref="IComplianceRecordRepository.TrySaveChangesAsync"/>.</summary>
public enum ComplianceSaveOutcome
{
    Saved,

    /// <summary>
    /// Another registration got there first: a DocumentSeries version token was stale, or a
    /// concurrent first registration already inserted the (series, year) row or the tenant's
    /// default settings. Nothing persisted, tracker cleared — retry and load the winner's row.
    /// </summary>
    ConcurrencyConflict,

    /// <summary>The unique (tenant, source_module, source_record_id) index rejected a duplicate registration; nothing persisted.</summary>
    DuplicateSource,
}

/// <summary>
/// Write-side persistence for the compliance record aggregate <em>and</em> the module's unit of
/// work: <see cref="SaveChangesAsync"/> / <see cref="TrySaveChangesAsync"/> flush everything the
/// other write-side repositories added to the same DbContext (series, settings, destruction log
/// lines), which is what makes "issue a number and register the record" one transaction.
/// Tenant-scoped through the query filter and RLS.
/// </summary>
public interface IComplianceRecordRepository
{
    Task<ComplianceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The record registered for this producer-side key, or null — the idempotency lookup.</summary>
    Task<ComplianceRecord?> GetBySourceAsync(string sourceModule, string sourceRecordId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplianceRecord>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Every Active or Superseded record of the tenant — the retention sweep's input.</summary>
    Task<IReadOnlyList<ComplianceRecord>> ListRetainedAsync(CancellationToken cancellationToken = default);

    void Add(ComplianceRecord record);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves like <see cref="SaveChangesAsync"/> but reports a lost race instead of throwing: a
    /// stale series version or a duplicate source key. On anything but Saved nothing was
    /// persisted and the unit of work has been cleared, so the caller re-reads and retries.
    /// </summary>
    Task<ComplianceSaveOutcome> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Write side for <see cref="DocumentSeries"/>; saves go through the record repository's unit of work.</summary>
public interface IDocumentSeriesRepository
{
    Task<DocumentSeries?> GetAsync(DocumentSeriesCode series, int year, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentSeries>> ListAsync(int year, CancellationToken cancellationToken = default);

    void Add(DocumentSeries series);
}

/// <summary>Write side for the one-per-tenant settings row; saves go through the record repository's unit of work.</summary>
public interface ITenantComplianceSettingsRepository
{
    Task<TenantComplianceSettings?> GetAsync(CancellationToken cancellationToken = default);

    void Add(TenantComplianceSettings settings);
}

/// <summary>Append-only destruction log; saves go through the record repository's unit of work.</summary>
public interface IDestructionLogRepository
{
    void Add(DestructionLogEntry entry);

    Task<IReadOnlyList<DestructionLogEntry>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}

/// <summary>
/// The published rule sets — reference rows with no tenant. Writes run under the platform's
/// system session (the table's RLS admits only that), which is why this repository saves itself
/// rather than joining the tenant unit of work.
/// </summary>
public interface IRuleSetRepository
{
    Task<RuleSet?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RuleSet?> FindAsync(string jurisdiction, int version, CancellationToken cancellationToken = default);

    /// <summary>The newest published version of the jurisdiction, or null.</summary>
    Task<RuleSet?> LatestAsync(string jurisdiction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleSet>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(RuleSet ruleSet, CancellationToken cancellationToken = default);
}

/// <summary>Write side for <see cref="TenantRuleSetApplication"/>; tenant-scoped.</summary>
public interface ITenantRuleSetApplicationRepository
{
    /// <summary>The newest application at or before <paramref name="asOfUtc"/>, or null when the tenant has applied nothing by then.</summary>
    Task<TenantRuleSetApplication?> LatestAsOfAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);

    void Add(TenantRuleSetApplication application);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The regulatory-basis catalogue — reference rows; upserts run under the system session.</summary>
public interface IRegulatoryBasisRepository
{
    Task<IReadOnlyList<Domain.RegulatoryBasis.RegulatoryBasis>> GetByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.RegulatoryBasis.RegulatoryBasis>> ListAsync(RegulatoryInstrument? instrument, CancellationToken cancellationToken = default);

    /// <summary>Inserts missing keys and overwrites existing ones from the definitions; returns how many rows were inserted.</summary>
    Task<int> UpsertAsync(IReadOnlyList<RegulatoryBasisDefinition> definitions, CancellationToken cancellationToken = default);
}
