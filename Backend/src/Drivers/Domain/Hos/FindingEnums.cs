namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>How serious a <see cref="ComplianceFinding"/> is. Member names travel on the wire.</summary>
public enum FindingSeverity
{
    Minor,
    Major,
    Severe,
}

/// <summary>
/// Workflow of a <see cref="ComplianceFinding"/>: <see cref="Open"/> → <see cref="Corrected"/> →
/// <see cref="Closed"/> (Open may close directly). Closed is terminal — a finding is never
/// deleted and never reopened; a recurrence is a new finding. Member names travel on the wire.
/// </summary>
public enum FindingStatus
{
    Open,
    Corrected,
    Closed,
}

/// <summary>Where a <see cref="ComplianceFinding"/> came from. Member names travel on the wire.</summary>
public enum FindingSource
{
    /// <summary>A compliance officer's review of the ledger.</summary>
    Audit,

    /// <summary>A roadside or facility inspection by enforcement.</summary>
    Enforcement,

    /// <summary>Opened by the platform itself (an out-of-service declaration opens one).</summary>
    Auto,
}
