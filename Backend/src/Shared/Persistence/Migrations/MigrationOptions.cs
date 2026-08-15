namespace NorthernLink.Shared.Persistence.Migrations;

/// <summary>
/// Config section for startup migrations. Non-secret, so this stays config-bound in
/// appsettings.json (unlike the connection string itself, which is environment-only).
/// </summary>
public sealed class MigrationOptions
{
    public const string SectionName = "Migrations";

    /// <summary>
    /// Defaults to FALSE deliberately. Every environment — orchestrated, standalone, production —
    /// points at the same managed Postgres, so a default of true would let any developer's
    /// <c>dotnet run</c> on a feature branch apply half-finished migrations to the live schema.
    /// Deployed containers opt in with <c>Migrations__RunOnStartup=true</c>; developers keep
    /// applying migrations by hand with <c>dotnet ef database update</c>.
    /// </summary>
    public bool RunOnStartup { get; set; }
}
