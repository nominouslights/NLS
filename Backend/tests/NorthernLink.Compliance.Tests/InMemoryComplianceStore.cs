using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Domain.RuleSets;

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
    IDestructionLogRepository
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
        public int ConcurrencyConflicts { get; set; }
    }

    private readonly SharedState _state;
    private readonly List<(DocumentSeries Instance, int LoadedVersion)> _loadedSeries = [];
    private readonly List<DocumentSeries> _pendingSeries = [];
    private readonly List<ComplianceRecord> _pendingRecords = [];
    private readonly List<TenantComplianceSettings> _pendingSettings = [];
    private readonly List<DestructionLogEntry> _pendingLog = [];

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

        Records.AddRange(_pendingRecords);
        Settings.AddRange(_pendingSettings);
        DestructionLog.AddRange(_pendingLog);

        foreach (var record in Records)
        {
            record.ClearDomainEvents();
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
    }

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
