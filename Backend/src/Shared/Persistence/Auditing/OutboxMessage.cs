namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Transactional-outbox row for an integration event: written to the module's
/// <c>outbox_messages</c> table in the same transaction as the aggregate save that
/// produced it, then published to RabbitMQ by <see cref="OutboxDispatcher{TDbContext}"/>.
/// The payload is the exact wire JSON and the routing key is precomputed, so the
/// dispatcher publishes bytes without knowing the CLR type. Rows are retained after
/// dispatch as part of the audit story; pruning is future work.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Database-generated identity; stable dispatch order within the module.</summary>
    public long Position { get; init; }

    /// <summary>The integration event's EventId — consumers' idempotency key.</summary>
    public required Guid Id { get; init; }

    public required Guid TenantId { get; init; }

    /// <summary>CLR type name for humans, e.g. "VehicleStatusChangedIntegrationEvent".</summary>
    public required string EventType { get; init; }

    /// <summary>Precomputed via EventRoutingKey, e.g. "fleet.vehicle-status-changed".</summary>
    public required string RoutingKey { get; init; }

    /// <summary>The integration event serialized with the wire JSON options (jsonb column).</summary>
    public required string Payload { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? DispatchedAtUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>Exponential-backoff gate; the dispatcher skips rows whose time hasn't come.</summary>
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
}
