namespace NorthernLink.Shared.EventBus;

/// <summary>Bound from the "OutboxPolling" configuration section; shared by every module's polling consumer.</summary>
public sealed class OutboxPollingOptions
{
    public const string SectionName = "OutboxPolling";

    /// <summary>How often each consuming module polls its producer outbox tables.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Rows fetched per producer schema per poll.</summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>
    /// After this many failed handler runs a row is marked Failed and later rows flow past
    /// it (park-and-continue) — visible to operators, re-runnable by resetting the status.
    /// </summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>
    /// Cap on the exponential retry backoff for a single failing ROW — how long one poisoned
    /// message waits before its next handler attempt, while the rest of the loop runs normally.
    /// Not to be confused with <see cref="MaxPollBackoffSeconds"/>.
    /// </summary>
    public int MaxBackoffSeconds { get; init; } = 60;

    /// <summary>
    /// Cap on how far the whole POLL LOOP backs off while consecutive polls keep failing
    /// (<c>PollBackoff</c>) — the database being unreachable, rather than one bad message.
    /// The row-level counterpart is <see cref="MaxBackoffSeconds"/>: that one paces retries of
    /// a message the handler rejects, this one paces retries of a poll that never got started.
    /// </summary>
    public int MaxPollBackoffSeconds { get; init; } = 60;
}
