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

    /// <summary>Cap on the exponential retry backoff.</summary>
    public int MaxBackoffSeconds { get; init; } = 60;
}
