using System.Xml.Linq;

namespace NorthernLink.ArchitectureTests;

/// <summary>
/// Discovers the solution's domain-library layout on disk. The project-reference rules are
/// checked against the .csproj files themselves (not compiled IL) so they hold even
/// while libraries are empty scaffolds — an illegal reference fails the build before
/// any code uses it.
/// </summary>
public static class ModuleGraph
{
    public static readonly string[] DomainNames =
    [
        "Identity", "Trips", "Drivers", "Fleet", "Clients",
        "Billing", "Incidents", "Notifications", "Grocery",
    ];

    /// <summary>src/ folders that are not domain libraries.</summary>
    private static readonly string[] NonDomainFolders = ["Api", "Shared"];

    public static string BackendRoot { get; } = FindBackendRoot();

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NorthernLink.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate NorthernLink.slnx above the test output directory.");
    }

    public static string DomainProjectPath(string domain) =>
        Path.Combine(BackendRoot, "src", domain, $"NorthernLink.{domain}.csproj");

    public static string SharedProjectPath { get; } =
        Path.Combine(BackendRoot, "src", "Shared", "NorthernLink.Shared.csproj");

    public static string ApiProjectPath { get; } =
        Path.Combine(BackendRoot, "src", "Api", "NorthernLink.Api", "NorthernLink.Api.csproj");

    /// <summary>Project names (file name without extension) referenced by a csproj.</summary>
    public static IReadOnlyList<string> ProjectReferences(string csprojPath) =>
        XDocument.Load(csprojPath)
            .Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .ToList();

    /// <summary>All domain libraries discovered on disk — catches one added without tests knowing.</summary>
    public static IReadOnlyList<string> DomainsOnDisk() =>
        Directory.GetDirectories(Path.Combine(BackendRoot, "src"))
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .Where(name => !NonDomainFolders.Contains(name))
            .OrderBy(name => name)
            .ToList();
}
