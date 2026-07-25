namespace NorthernLink.Trips.Infrastructure.Generation;

/// <summary>
/// Bound from the "TripGeneration" configuration section (non-secret tuning values only).
/// </summary>
public sealed class TripGenerationOptions
{
    public const string SectionName = "TripGeneration";

    /// <summary>How often the worker expands active templates into trips.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Delay before the first pass — long enough to skip the boot race (same reasoning
    /// as the outbox/projection workers), short enough that a fresh template shows up on
    /// the DispatchBoard shortly after startup.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(15);
}
