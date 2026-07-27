namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read side for the trip activity timeline — pulls raw rows from the module's append-only
/// <c>event_journal</c> for a trip and (optionally) its attached manifest. Read-only:
/// implementations only SELECT, never write the journal. Tenant-scoped (EF global query
/// filter + Postgres RLS), like every other Trips read.
/// </summary>
public interface ITripActivityReadService
{
    /// <summary>
    /// The union of journal rows for the trip aggregate (<paramref name="tripId"/>) and, when
    /// <paramref name="manifestId"/> is set, its manifest aggregate. Ordering is applied by the
    /// caller; implementations return the raw union.
    /// </summary>
    Task<IReadOnlyList<TripActivityJournalEntry>> GetJournalEntriesAsync(
        Guid tripId,
        Guid? manifestId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single journal row projected to just the fields the activity timeline needs. The
/// application-layer shape so the read abstraction never leaks the persistence entity;
/// <see cref="Payload"/> is the raw event JSON the handler mines for manifest provenance.
/// </summary>
public sealed record TripActivityJournalEntry(
    DateTimeOffset OccurredAtUtc,
    string AggregateType,
    string EventType,
    string Payload);
