using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Application.Records;

/// <summary>
/// The one place a record gets its number. Both <c>RegisterRecord</c> and <c>SupersedeRecord</c>
/// run this loop: read the tenant's settings (creating the Northern Link defaults for a tenant
/// nobody has seeded — a producer's event must never park because an admin forgot a POST), read
/// or open the (series, year) counter, issue the next number, build the record, and save
/// <b>both in one SaveChanges</b>. A stale series version fails that save as a whole, so the loser
/// re-reads and tries again with the next number — consecutive, never skipped. A duplicate
/// source key (another request registered the same producer record first) ends the loop by
/// re-reading the winner. <c>NL-SP-02 s. 11; HTA s. 318.8(1)</c>.
/// </summary>
public sealed class RecordRegistrar(
    IComplianceRecordRepository records,
    IDocumentSeriesRepository series,
    ITenantComplianceSettingsRepository settings)
{
    public const int MaxAttempts = 5;

    /// <summary>Registers a new record; idempotent on (SourceModule, SourceRecordId).</summary>
    public Task<Result<RegisterRecordResult>> RegisterAsync(
        Guid tenantId,
        RecordTypeDefinition definition,
        RecordRegistration registration,
        Guid? actorId,
        CancellationToken cancellationToken) =>
        IssueAndSaveAsync(
            tenantId,
            registration,
            async attempt => ComplianceRecord.Register(
                tenantId, definition, await attempt.IssueAsync(definition), registration, actorId, attempt.Now),
            cancellationToken);

    /// <summary>Registers a successor version of <paramref name="predecessorId"/> and retires the predecessor in the same save.</summary>
    public Task<Result<RegisterRecordResult>> SupersedeAsync(
        Guid tenantId,
        Guid predecessorId,
        RecordRegistration registration,
        Guid? actorId,
        CancellationToken cancellationToken) =>
        IssueAndSaveAsync(
            tenantId,
            registration,
            async attempt =>
            {
                // Re-read inside every attempt: a cleared tracker must never leave a detached
                // predecessor half-superseded in memory.
                var predecessor = await records.GetByIdAsync(predecessorId, cancellationToken);
                if (predecessor is null)
                {
                    return Result.Failure<ComplianceRecord>(ComplianceRecordErrors.NotFound);
                }

                var definition = ComplianceRecordCatalogue.For(predecessor.RecordType);
                return predecessor.Supersede(definition, await attempt.IssueAsync(definition), registration, actorId, attempt.Now);
            },
            cancellationToken);

    /// <summary>What one attempt of the loop offers the record builder: the clock, and a number issued on demand.</summary>
    public sealed class Attempt
    {
        private readonly Func<RecordTypeDefinition, Task<string>> _issue;
        private string? _issued;

        internal Attempt(DateTimeOffset now, Func<RecordTypeDefinition, Task<string>> issue)
        {
            Now = now;
            _issue = issue;
        }

        public DateTimeOffset Now { get; }

        /// <summary>Issues the next number of the definition's series (once per attempt — a second call returns the same number).</summary>
        public async Task<string> IssueAsync(RecordTypeDefinition definition) => _issued ??= await _issue(definition);
    }

    private async Task<Result<RegisterRecordResult>> IssueAndSaveAsync(
        Guid tenantId,
        RecordRegistration registration,
        Func<Attempt, Task<Result<ComplianceRecord>>> buildAsync,
        CancellationToken cancellationToken)
    {
        var sourceModule = registration.SourceModule?.Trim() ?? string.Empty;
        var sourceRecordId = registration.SourceRecordId?.Trim() ?? string.Empty;

        for (var attemptNumber = 1; attemptNumber <= MaxAttempts; attemptNumber++)
        {
            // Idempotency first: the producer-side key already registered → that record, unchanged.
            if (sourceModule.Length > 0 && sourceRecordId.Length > 0)
            {
                var existing = await records.GetBySourceAsync(sourceModule, sourceRecordId, cancellationToken);
                if (existing is not null)
                {
                    return Result.Success(new RegisterRecordResult(existing.Id, existing.DocumentNumber, Created: false));
                }
            }

            var tenantSettings = await settings.GetAsync(cancellationToken);
            if (tenantSettings is null)
            {
                tenantSettings = TenantComplianceSettings.CreateDefault(tenantId, actorId: null);
                settings.Add(tenantSettings);
            }

            var now = DateTimeOffset.UtcNow;
            var year = now.Year;
            var prefix = tenantSettings.DocumentPrefix;

            // The counter is read lazily, inside Issue, because the series depends on the record
            // type — which Supersede only knows after re-reading the predecessor.
            var attempt = new Attempt(now, async definition =>
            {
                var counter = await series.GetAsync(definition.Series, year, cancellationToken);
                if (counter is null)
                {
                    counter = DocumentSeries.Open(tenantId, definition.Series, year);
                    series.Add(counter);
                }

                return counter.IssueNext(prefix, definition.FormNumber);
            });

            var built = await buildAsync(attempt);
            if (built.IsFailure)
            {
                // Nothing this attempt staged may survive: an un-saved series bump or default
                // settings would otherwise ride the scope's next SaveChanges.
                await DiscardAsync(cancellationToken);
                return Result.Failure<RegisterRecordResult>(built.Error);
            }

            records.Add(built.Value);

            switch (await records.TrySaveChangesAsync(cancellationToken))
            {
                case ComplianceSaveOutcome.Saved:
                    return Result.Success(new RegisterRecordResult(built.Value.Id, built.Value.DocumentNumber, Created: true));
                case ComplianceSaveOutcome.DuplicateSource:
                case ComplianceSaveOutcome.ConcurrencyConflict:
                default:
                    continue; // re-read: a duplicate is found by the idempotency lookup, a stale counter reloads
            }
        }

        return Result.Failure<RegisterRecordResult>(DocumentSeriesErrors.ConcurrencyExhausted);
    }

    private Task DiscardAsync(CancellationToken cancellationToken) =>
        records is IDiscardableUnitOfWork discardable ? discardable.DiscardAsync(cancellationToken) : Task.CompletedTask;
}

/// <summary>
/// Optional capability of a unit of work: forget everything staged since the last save. The EF
/// repository implements it with <c>ChangeTracker.Clear()</c>; the in-memory test store drops its
/// pending adds.
/// </summary>
public interface IDiscardableUnitOfWork
{
    Task DiscardAsync(CancellationToken cancellationToken = default);
}
