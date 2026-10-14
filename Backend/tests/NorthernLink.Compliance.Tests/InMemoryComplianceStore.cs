using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.Registrations;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Domain.RuleSets;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// One in-memory unit of work standing in for the module DbContext: it implements every tenant
/// write-side repository the handlers take, so "series bump and record insert in one save" is
/// modelled faithfully. Every read is scoped to <see cref="QueryFilterTenantId"/>.
/// <para>
/// <b>The document series carries real optimistic concurrency.</b> <see cref="GetAsync"/> hands
/// out a fresh rehydrated instance per load (as EF would per request) and remembers the committed
/// version it was loaded at; <see cref="TrySaveChangesAsync"/> refuses the save when the committed
/// version has moved — exactly the <c>DbUpdateConcurrencyException</c> path — and drops everything
/// pending. <see cref="BeforeNextSave"/> lets a test interleave a second registration inside the
/// first one's save, which is how the race is produced deterministically.
/// </para>
/// </summary>
internal sealed class InMemoryComplianceStore :
    IComplianceRecordRepository,
    IDiscardableUnitOfWork,
    IDocumentSeriesRepository,
    ITenantComplianceSettingsRepository,
    IDestructionLogRepository,
    ICarrierAuthorityRepository,
    IInsurancePolicyRepository,
    IVehicleRegistrationRepository
{
    internal sealed record CommittedSeries(Guid Id, int LastSequence, int Version);

    /// <summary>
    /// The "database": committed state shared by every store built over it. Two stores over one
    /// state are two requests' DbContexts over one Postgres — each with its own pending changes.
    /// </summary>
    internal sealed class SharedState
    {
        public Dictionary<(Guid TenantId, DocumentSeriesCode Series, int Year), CommittedSeries> Series { get; } = [];
        public List<ComplianceRecord> Records { get; } = [];
        public List<TenantComplianceSettings> Settings { get; } = [];
        public List<DestructionLogEntry> DestructionLog { get; } = [];
        public List<CarrierAuthority> Authorities { get; } = [];
        public List<InsurancePolicy> Policies { get; } = [];
        public List<VehicleRegistration> Registrations { get; } = [];

        /// <summary>Every domain event a successful save committed, in order — what the mapper would have seen.</summary>
        public List<IDomainEvent> SavedEvents { get; } = [];

        public int ConcurrencyConflicts { get; set; }
    }

    private readonly SharedState _state;
    private readonly List<(DocumentSeries Instance, int LoadedVersion)> _loadedSeries = [];
    private readonly List<DocumentSeries> _pendingSeries = [];
    private readonly List<ComplianceRecord> _pendingRecords = [];
    private readonly List<TenantComplianceSettings> _pendingSettings = [];
    private readonly List<DestructionLogEntry> _pendingLog = [];
    private readonly List<CarrierAuthority> _pendingAuthorities = [];
    private readonly List<InsurancePolicy> _pendingPolicies = [];
    private readonly List<VehicleRegistration> _pendingRegistrations = [];

    public InMemoryComplianceStore() : this(new SharedState())
    {
    }

    public InMemoryComplianceStore(SharedState state)
    {
        _state = state;
    }

    public SharedState State => _state;
    public List<ComplianceRecord> Records => _state.Records;
    public List<TenantComplianceSettings> Settings => _state.Settings;
    public List<DestructionLogEntry> DestructionLog => _state.DestructionLog;
    public List<CarrierAuthority> Authorities => _state.Authorities;
    public List<InsurancePolicy> Policies => _state.Policies;
    public List<VehicleRegistration> Registrations => _state.Registrations;
    public List<IDomainEvent> SavedEvents => _state.SavedEvents;

    public int SaveAttempts { get; private set; }
    public int ConcurrencyConflicts => _state.ConcurrencyConflicts;

    /// <summary>The ambient tenant the real DbContext's query filter would compare against.</summary>
    public Guid QueryFilterTenantId { get; set; } = TestCompliance.TenantId;

    /// <summary>Runs once, inside the next TrySave, before the version check — the hook that produces a lost race.</summary>
    public Func<Task>? BeforeNextSave { get; set; }

    public int CommittedLastSequence(DocumentSeriesCode series, int year) =>
        _state.Series.TryGetValue((QueryFilterTenantId, series, year), out var committed) ? committed.LastSequence : 0;

    // --- IComplianceRecordRepository ---------------------------------------------------------------

    private IEnumerable<ComplianceRecord> VisibleRecords => Records.Where(r => r.TenantId == QueryFilterTenantId);

    public Task<ComplianceRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(VisibleRecords.FirstOrDefault(r => r.Id == id));

    public Task<ComplianceRecord?> GetBySourceAsync(string sourceModule, string sourceRecordId, CancellationToken cancellationToken = default) =>
        Task.FromResult(VisibleRecords.FirstOrDefault(r => r.SourceModule == sourceModule && r.SourceRecordId == sourceRecordId));

    public Task<IReadOnlyList<ComplianceRecord>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ComplianceRecord>>(VisibleRecords.Where(r => ids.Contains(r.Id)).ToList());

    public Task<IReadOnlyList<ComplianceRecord>> ListRetainedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ComplianceRecord>>(VisibleRecords
            .Where(r => r.Status is ComplianceRecordStatus.Active or ComplianceRecordStatus.Superseded)
            .ToList());

    public void Add(ComplianceRecord record) => _pendingRecords.Add(record);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var outcome = await TrySaveChangesAsync(cancellationToken);
        if (outcome != ComplianceSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Save failed: {outcome}");
        }
    }

    public async Task<ComplianceSaveOutcome> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveAttempts++;

        if (BeforeNextSave is { } hook)
        {
            BeforeNextSave = null;
            await hook();
        }

        // Version check on every loaded series — the concurrency token.
        foreach (var (instance, loadedVersion) in _loadedSeries)
        {
            var committed = _state.Series[(instance.TenantId, instance.Series, instance.Year)];
            if (committed.Version != loadedVersion)
            {
                _state.ConcurrencyConflicts++;
                Discard();
                return ComplianceSaveOutcome.ConcurrencyConflict;
            }
        }

        // Unique (tenant, series, year) and unique settings tenant_id: a concurrent first
        // registration already inserted the row this unit of work also tried to insert. The EF
        // repository maps both unique violations to ConcurrencyConflict.
        if (_pendingSeries.Any(s => _state.Series.ContainsKey((s.TenantId, s.Series, s.Year)))
            || _pendingSettings.Any(p => Settings.Any(s => s.TenantId == p.TenantId)))
        {
            _state.ConcurrencyConflicts++;
            Discard();
            return ComplianceSaveOutcome.ConcurrencyConflict;
        }

        // Unique (tenant, source_module, source_record_id).
        foreach (var pending in _pendingRecords)
        {
            if (Records.Any(r => r.TenantId == pending.TenantId && r.SourceModule == pending.SourceModule && r.SourceRecordId == pending.SourceRecordId))
            {
                Discard();
                return ComplianceSaveOutcome.DuplicateSource;
            }
        }

        foreach (var (instance, loadedVersion) in _loadedSeries)
        {
            var key = (instance.TenantId, instance.Series, instance.Year);
            _state.Series[key] = new CommittedSeries(instance.Id, instance.LastSequence, loadedVersion + 1);
        }

        foreach (var opened in _pendingSeries)
        {
            _state.Series[(opened.TenantId, opened.Series, opened.Year)] = new CommittedSeries(opened.Id, opened.LastSequence, 1);
        }

        // One authority per tenant; one Current policy / registration per vehicle — the unique
        // and partial unique indexes. EF would throw a DbUpdateException (a 500); so does this.
        if (_pendingAuthorities.Any(p => Authorities.Any(a => a.TenantId == p.TenantId)))
        {
            Discard();
            throw new InvalidOperationException("Unique violation: IX_carrier_authorities_tenant_id");
        }

        if (Policies.Concat(_pendingPolicies)
                .Where(p => p.Status == AuthorityDocumentStatus.Current)
                .GroupBy(p => (p.TenantId, p.VehicleId))
                .Any(g => g.Count() > 1)
            || Registrations.Concat(_pendingRegistrations)
                .Where(r => r.Status == AuthorityDocumentStatus.Current)
                .GroupBy(r => (r.TenantId, r.VehicleId))
                .Any(g => g.Count() > 1))
        {
            Discard();
            throw new InvalidOperationException("Unique violation: one Current per vehicle");
        }

        Records.AddRange(_pendingRecords);
        Settings.AddRange(_pendingSettings);
        DestructionLog.AddRange(_pendingLog);
        Authorities.AddRange(_pendingAuthorities);
        Policies.AddRange(_pendingPolicies);
        Registrations.AddRange(_pendingRegistrations);

        IEnumerable<AggregateRoot> committedAggregates =
            [.. Records, .. Settings, .. Authorities, .. Policies, .. Registrations];
        foreach (var aggregate in committedAggregates)
        {
            SavedEvents.AddRange(aggregate.DomainEvents);
            aggregate.ClearDomainEvents();
        }

        ClearPending();
        return ComplianceSaveOutcome.Saved;
    }

    public Task DiscardAsync(CancellationToken cancellationToken = default)
    {
        Discard();
        return Task.CompletedTask;
    }

    private void Discard() => ClearPending();

    private void ClearPending()
    {
        _loadedSeries.Clear();
        _pendingSeries.Clear();
        _pendingRecords.Clear();
        _pendingSettings.Clear();
        _pendingLog.Clear();
        _pendingAuthorities.Clear();
        _pendingPolicies.Clear();
        _pendingRegistrations.Clear();
    }

    // --- Epic 2 aggregates -------------------------------------------------------------------------

    Task<CarrierAuthority?> ICarrierAuthorityRepository.GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Authorities.Concat(_pendingAuthorities).FirstOrDefault(a => a.TenantId == QueryFilterTenantId));

    public void Add(CarrierAuthority authority) => _pendingAuthorities.Add(authority);

    Task<InsurancePolicy?> IInsurancePolicyRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Policies.FirstOrDefault(p => p.TenantId == QueryFilterTenantId && p.Id == id));

    Task<InsurancePolicy?> IInsurancePolicyRepository.GetCurrentForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        Task.FromResult(Policies.FirstOrDefault(p =>
            p.TenantId == QueryFilterTenantId && p.VehicleId == vehicleId && p.Status == AuthorityDocumentStatus.Current));

    Task<IReadOnlyList<InsurancePolicy>> IInsurancePolicyRepository.ListAsync(Guid? vehicleId, bool includeSuperseded, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InsurancePolicy>>(Policies
            .Where(p => p.TenantId == QueryFilterTenantId
                && (vehicleId is null || p.VehicleId == vehicleId)
                && (includeSuperseded || p.Status == AuthorityDocumentStatus.Current))
            .ToList());

    public void Add(InsurancePolicy policy) => _pendingPolicies.Add(policy);

    Task<VehicleRegistration?> IVehicleRegistrationRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Registrations.FirstOrDefault(r => r.TenantId == QueryFilterTenantId && r.Id == id));

    Task<VehicleRegistration?> IVehicleRegistrationRepository.GetCurrentForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        Task.FromResult(Registrations.FirstOrDefault(r =>
            r.TenantId == QueryFilterTenantId && r.VehicleId == vehicleId && r.Status == AuthorityDocumentStatus.Current));

    Task<IReadOnlyList<VehicleRegistration>> IVehicleRegistrationRepository.ListAsync(Guid? vehicleId, bool includeSuperseded, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<VehicleRegistration>>(Registrations
            .Where(r => r.TenantId == QueryFilterTenantId
                && (vehicleId is null || r.VehicleId == vehicleId)
                && (includeSuperseded || r.Status == AuthorityDocumentStatus.Current))
            .ToList());

    public void Add(VehicleRegistration registration) => _pendingRegistrations.Add(registration);

    // --- IDocumentSeriesRepository -----------------------------------------------------------------

    public Task<DocumentSeries?> GetAsync(DocumentSeriesCode series, int year, CancellationToken cancellationToken = default)
    {
        if (!_state.Series.TryGetValue((QueryFilterTenantId, series, year), out var committed))
        {
            return Task.FromResult<DocumentSeries?>(null);
        }

        var instance = DocumentSeries.Rehydrate(committed.Id, QueryFilterTenantId, series, year, committed.LastSequence);
        _loadedSeries.Add((instance, committed.Version));
        return Task.FromResult<DocumentSeries?>(instance);
    }

    public Task<IReadOnlyList<DocumentSeries>> ListAsync(int year, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DocumentSeries>>(_state.Series
            .Where(pair => pair.Key.TenantId == QueryFilterTenantId && pair.Key.Year == year)
            .Select(pair => DocumentSeries.Rehydrate(pair.Value.Id, pair.Key.TenantId, pair.Key.Series, pair.Key.Year, pair.Value.LastSequence))
            .ToList());

    public void Add(DocumentSeries series) => _pendingSeries.Add(series);

    // --- ITenantComplianceSettingsRepository -------------------------------------------------------

    Task<TenantComplianceSettings?> ITenantComplianceSettingsRepository.GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Settings.FirstOrDefault(s => s.TenantId == QueryFilterTenantId));

    public void Add(TenantComplianceSettings settings) => _pendingSettings.Add(settings);

    // --- IDestructionLogRepository -----------------------------------------------------------------

    public void Add(DestructionLogEntry entry) => _pendingLog.Add(entry);

    Task<IReadOnlyList<DestructionLogEntry>> IDestructionLogRepository.ListAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DestructionLogEntry>>(DestructionLog.Where(d => d.TenantId == QueryFilterTenantId).ToList());
}

/// <summary>In-memory reference store for rule sets and the basis catalogue (no tenant).</summary>
internal sealed class InMemoryReferenceStore : IRuleSetRepository, IRegulatoryBasisRepository
{
    public List<RuleSet> RuleSets { get; } = [];
    public List<RegulatoryBasis> Basis { get; } = [];

    public Task<RuleSet?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(RuleSets.FirstOrDefault(r => r.Id == id));

    public Task<RuleSet?> FindAsync(string jurisdiction, int version, CancellationToken cancellationToken = default) =>
        Task.FromResult(RuleSets.FirstOrDefault(r => r.Jurisdiction == jurisdiction && r.Version == version));

    public Task<RuleSet?> LatestAsync(string jurisdiction, CancellationToken cancellationToken = default) =>
        Task.FromResult(RuleSets.Where(r => r.Jurisdiction == jurisdiction).OrderByDescending(r => r.Version).FirstOrDefault());

    public Task<IReadOnlyList<RuleSet>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RuleSet>>(RuleSets.ToList());

    public Task AddAsync(RuleSet ruleSet, CancellationToken cancellationToken = default)
    {
        RuleSets.Add(ruleSet);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RegulatoryBasis>> GetByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RegulatoryBasis>>(Basis.Where(b => keys.Contains(b.Key)).ToList());

    public Task<IReadOnlyList<RegulatoryBasis>> ListAsync(RegulatoryInstrument? instrument, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RegulatoryBasis>>(Basis.Where(b => instrument is null || b.Instrument == instrument).ToList());

    public Task<int> UpsertAsync(IReadOnlyList<RegulatoryBasisDefinition> definitions, CancellationToken cancellationToken = default)
    {
        var inserted = 0;
        foreach (var definition in definitions)
        {
            var existing = Basis.FirstOrDefault(b => b.Key == definition.Key);
            if (existing is null)
            {
                Basis.Add(RegulatoryBasis.Create(definition));
                inserted++;
            }
            else
            {
                existing.Overwrite(definition);
            }
        }

        return Task.FromResult(inserted);
    }
}

/// <summary>
/// In-memory tenant rule-set applications, scoped like the query filter. In production this
/// repository shares the module DbContext, so a save through <em>any</em> repository commits an
/// added application (the seed handler relies on that); the fake therefore commits on Add.
/// </summary>
internal sealed class InMemoryRuleSetApplicationRepository : ITenantRuleSetApplicationRepository
{
    public List<TenantRuleSetApplication> Applications { get; } = [];

    public Guid QueryFilterTenantId { get; set; } = TestCompliance.TenantId;

    public Task<TenantRuleSetApplication?> LatestAsOfAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(Applications
            .Where(a => a.TenantId == QueryFilterTenantId && a.AppliedAtUtc <= asOfUtc)
            .OrderByDescending(a => a.AppliedAtUtc)
            .FirstOrDefault());

    public void Add(TenantRuleSetApplication application) => Applications.Add(application);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
