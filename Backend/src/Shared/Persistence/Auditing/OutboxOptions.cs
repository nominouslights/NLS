namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>Bound from the "Outbox" configuration section; shared by every module's dispatcher.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often each module's dispatcher polls its outbox table.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Rows fetched per poll.</summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>
    /// After this many failed publishes a row is left in the table as poison — excluded
    /// from polling, visible to operators. There is no dead-letter table yet.
    /// </summary>
    public int MaxAttempts { get; init; } = 8;

    /// <summary>
    /// Cap on how far the poll loop backs off while consecutive polls keep failing
    /// (<c>PollBackoff</c>). This is about the loop, not a row: a broker or database outage
    /// fails every poll, and retrying it every <see cref="PollInterval"/> only floods the log.
    /// </summary>
    public int MaxPollBackoffSeconds { get; init; } = 60;
}
