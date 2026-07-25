namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>Bound from the "Projections" configuration section; shared by every module's projection worker.</summary>
public sealed class ProjectionOptions
{
    public const string SectionName = "Projections";

    /// <summary>How often each module's projection worker polls its event journal.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Journal rows fetched per poll.</summary>
    public int BatchSize { get; init; } = 200;
}
