namespace NorthernLink.Shared.Persistence.Migrations;

/// <summary>
/// The one bit of state <see cref="ModuleMigrationRunner"/> publishes to the rest of the host:
/// has the schema been brought up to date yet? Registered as a singleton and read by the
/// gateway's readiness health check, which is what keeps a container out of the load balancer
/// until migrations finish — Kestrel starts listening before any <c>IHostedService</c> runs,
/// so readiness, not startup ordering, is the real gate.
/// </summary>
public sealed class MigrationState
{
    private volatile bool _completed;
    private volatile string? _failure;

    public bool Completed => _completed;

    /// <summary>Non-null once a migration attempt has failed; surfaced in the readiness payload.</summary>
    public string? Failure => _failure;

    public void MarkCompleted()
    {
        _failure = null;
        _completed = true;
    }

    public void MarkFailed(string reason)
    {
        _failure = reason;
        _completed = false;
    }
}
