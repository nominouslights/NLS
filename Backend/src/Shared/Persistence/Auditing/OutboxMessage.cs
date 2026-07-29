namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Transactional-outbox row for an integration event: written to the module's
/// <c>outbox_messages</c> table in the same transaction as the aggregate save that
/// produced it. The payload is the exact wire JSON and the routing key is precomputed,
/// so delivery works on bytes without knowing the CLR type. Rows are retained
/// indefinitely as part of the audit story; pruning is future work and must respect
/// <see cref="ProcessingStatus"/> (a row is only safe to prune once processed).
///
/// Two delivery paths read this table:
/// storing/projecting events are consumed in-database by each consuming module's
/// <c>OutboxPollingConsumer</c>, tracked by the <c>Processing*</c> columns; chain-reaction
/// events (routing keys in <c>BusPublicationRegistry</c>) are additionally published to
/// RabbitMQ by <see cref="OutboxDispatcher{TDbContext}"/>, tracked by
/// <see cref="DispatchedAtUtc"/>/<see cref="Attempts"/>/<see cref="LastError"/>/
/// <see cref="NextAttemptAtUtc"/> — those four belong exclusively to the bus path.
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

    /// <summary>
    /// In-database consumption state, advanced by the consuming module's poller. Defaults to
    /// Pending — including for pre-existing rows when the column is introduced, which is what
    /// makes the first poll replay all history through the (idempotent) handlers.
    /// </summary>
    public OutboxProcessingStatus ProcessingStatus { get; set; } = OutboxProcessingStatus.Pending;

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public int ProcessingAttempts { get; set; }

    public string? ProcessingLastError { get; set; }

    /// <summary>Backoff gate for the polling consumer; mirrors <see cref="NextAttemptAtUtc"/>.</summary>
    public DateTimeOffset? ProcessingNextAttemptAtUtc { get; set; }
}
