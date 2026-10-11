namespace NorthernLink.Budgeting.Domain.Qbo;

/// <summary>
/// Which Intuit environment a connection was made against. A sandbox company and a production
/// company never share a realm id, so the value is recorded for display and diagnosis, not used
/// as a key. Persisted by name (<c>varchar(16)</c>).
/// </summary>
public enum QboEnvironment
{
    Sandbox,
    Production,
}
