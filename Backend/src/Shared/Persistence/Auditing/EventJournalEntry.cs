namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// One row per domain event, appended to the module's <c>event_journal</c> table in the
/// same transaction as the aggregate save that raised it. Append-only by design — the
/// RLS policies grant SELECT and INSERT only, so the app role cannot rewrite history.
/// Deliberately not an <see cref="Kernel.AggregateRoot"/>: audit rows never re-enter
/// the audit pipeline.
/// </summary>
public sealed class EventJournalEntry
{
    /// <summary>Database-generated identity; total order of events within the module.</summary>
    public long Position { get; init; }

    public required Guid EventId { get; init; }

    public required Guid TenantId { get; init; }

    /// <summary>Stable kebab-cased aggregate name, e.g. "vehicle". See <see cref="AuditNames"/>.</summary>
    public required string AggregateType { get; init; }

    public required Guid AggregateId { get; init; }

    /// <summary>
    /// The aggregate version this save produced — links the event to the
    /// <see cref="AggregateSnapshot"/> written in the same transaction.
    /// </summary>
    public required int AggregateVersion { get; init; }

    /// <summary>Stable kebab-cased event name, e.g. "vehicle-status-changed". See <see cref="AuditNames"/>.</summary>
    public required string EventType { get; init; }

    /// <summary>The domain event serialized as JSON (jsonb column).</summary>
    public required string Payload { get; init; }

    /// <summary>When the domain raised the event (from IDomainEvent.OccurredAtUtc).</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>When the save pipeline wrote the row.</summary>
    public required DateTimeOffset RecordedAtUtc { get; init; }

    /// <summary>Null until the Identity module issues authenticated user ids.</summary>
    public Guid? ActorId { get; init; }

    /// <summary>The current request's trace id (Activity.TraceId), when one exists.</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>Reserved for command-level causation tracking; null until that exists.</summary>
    public Guid? CausationId { get; init; }
}
