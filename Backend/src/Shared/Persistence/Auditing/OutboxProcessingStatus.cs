namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// In-database consumption state of an outbox row, advanced by the consuming module's
/// <c>OutboxPollingConsumer</c>. Stored as text (Pending | Processed | Failed).
///
/// Invariant: the status column is single-consumer — every event type has exactly one
/// consuming module today, so one column can represent "processed". If a second module
/// ever needs to consume the same event type, this must become per-consumer state
/// (cursors or a status-per-consumer table); do not point a second poller at an
/// already-consumed routing key.
/// </summary>
public enum OutboxProcessingStatus
{
    /// <summary>Not yet consumed; picked up by the next poll.</summary>
    Pending,

    /// <summary>All handlers ran to completion.</summary>
    Processed,

    /// <summary>
    /// Parked after exhausting retry attempts (or on a permanently-broken payload).
    /// Visible to operators; re-runnable by resetting the status to Pending — when doing
    /// so for a state-carrying "changed" event, also reset any later Processed rows with
    /// the same routing key so the in-order replay converges back to the latest state.
    /// </summary>
    Failed,
}
