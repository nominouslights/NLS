namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// A full JSON snapshot of an aggregate's state, appended to the module's
/// <c>aggregate_snapshots</c> table in the same transaction as every save. Each row is
/// one moment in time: the future audit viewer scrolls an aggregate's history by reading
/// its snapshots ordered by <see cref="Version"/>, with <see cref="EventJournalEntry"/>
/// rows annotating what changed and why. Append-only (SELECT/INSERT-only RLS policies);
/// viewer-only documents, never deserialized for rehydration — the EF state tables stay
/// the source of truth.
/// </summary>
public sealed class AggregateSnapshot
{
    public required Guid AggregateId { get; init; }

    /// <summary>Monotonic per-aggregate version; (AggregateId, Version) is the primary key.</summary>
    public required int Version { get; init; }

    public required Guid TenantId { get; init; }

    /// <summary>Stable kebab-cased aggregate name, e.g. "vehicle". See <see cref="AuditNames"/>.</summary>
    public required string AggregateType { get; init; }

    /// <summary>The aggregate serialized as JSON (jsonb column), computed properties included.</summary>
    public required string State { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Null until the Identity module issues authenticated user ids.</summary>
    public Guid? ActorId { get; init; }

    /// <summary>The current request's trace id (Activity.TraceId), when one exists.</summary>
    public Guid? CorrelationId { get; init; }
}
