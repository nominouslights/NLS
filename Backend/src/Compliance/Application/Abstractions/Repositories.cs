using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.Registrations;
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

/// <summary>Write side for the one-per-tenant carrier authority; saves go through the record repository's unit of work.</summary>
public interface ICarrierAuthorityRepository
{
    Task<CarrierAuthority?> GetAsync(CancellationToken cancellationToken = default);

    void Add(CarrierAuthority authority);
}

/// <summary>Write side (and, until a read model exists, read side) for insurance policies; tenant-scoped.</summary>
public interface IInsurancePolicyRepository
{
    Task<InsurancePolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<InsurancePolicy?> GetCurrentForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Newest first; Current only unless <paramref name="includeSuperseded"/>.</summary>
    Task<IReadOnlyList<InsurancePolicy>> ListAsync(Guid? vehicleId, bool includeSuperseded, CancellationToken cancellationToken = default);

    void Add(InsurancePolicy policy);
}

/// <summary>Write side (and read side) for vehicle registrations; tenant-scoped.</summary>
public interface IVehicleRegistrationRepository
{
    Task<VehicleRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<VehicleRegistration?> GetCurrentForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleRegistration>> ListAsync(Guid? vehicleId, bool includeSuperseded, CancellationToken cancellationToken = default);

    void Add(VehicleRegistration registration);
}

/// <summary>
/// The Fleet vehicle replica. <see cref="UpsertAsync"/> and <see cref="SetRegulatedOverrideAsync"/>
/// save themselves (replica writes are not part of an aggregate's unit of work — the Budgeting
/// user_lookup precedent).
/// </summary>
public interface IVehicleLookupRepository
{
    Task<VehicleLookup?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleLookup>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts or overwrites every event-carried column; never touches <see cref="VehicleLookup.RegulatedOverride"/>.</summary>
    Task UpsertAsync(VehicleLookup vehicle, CancellationToken cancellationToken = default);

    /// <summary>Sets the Compliance-only override; false when the vehicle is not in the replica.</summary>
    Task<bool> SetRegulatedOverrideAsync(Guid vehicleId, bool? regulatedOverride, CancellationToken cancellationToken = default);
}

/// <summary>The Drivers replica; the upsert saves itself and returns the row as it was before (null when new).</summary>
public interface IDriverLookupRepository
{
    Task<DriverLookup?> GetAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<DriverLookup?> UpsertAsync(DriverLookup driver, CancellationToken cancellationToken = default);
}

/// <summary>The Identity account replica; the upsert saves itself.</summary>
public interface IUserLookupRepository
{
    Task<UserLookup?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default);
}
