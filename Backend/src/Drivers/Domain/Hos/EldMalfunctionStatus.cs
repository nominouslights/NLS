namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>Lifecycle of an <see cref="EldMalfunctionReport"/>. Member names travel on the wire.</summary>
public enum EldMalfunctionStatus
{
    /// <summary>The ELD is (or was, from <c>NoticedAtUtc</c>) not recording; paper logs are the sanctioned fallback.</summary>
    Open,

    /// <summary>A compliance officer confirmed the repair.</summary>
    Resolved,
}
