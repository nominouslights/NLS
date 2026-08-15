namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>Bound from the "Projections" configuration section; shared by every module's projection worker.</summary>
public sealed class ProjectionOptions
{
    public const string SectionName = "Projections";

    /// <summary>How often each module's projection worker polls its event journal.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Journal rows fetched per poll.</summary>
    public int BatchSize { get; init; } = 200;

    /// <summary>
    /// Cap on how far the poll loop backs off while consecutive polls keep failing
    /// (<c>PollBackoff</c>). A projection that cannot reach the database gains nothing by
    /// asking again every <see cref="PollInterval"/>; the journal is durable and waits.
    /// </summary>
    public int MaxPollBackoffSeconds { get; init; } = 60;
}
