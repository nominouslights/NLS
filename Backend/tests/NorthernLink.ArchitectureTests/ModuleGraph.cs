using System.Xml.Linq;

namespace NorthernLink.ArchitectureTests;

/// <summary>
/// Discovers the solution's module layout on disk. The project-reference rules are
/// checked against the .csproj files themselves (not compiled IL) so they hold even
/// while modules are empty scaffolds — an illegal reference fails the build before
/// any code uses it.
/// </summary>
public static class ModuleGraph
{
    public static readonly string[] ModuleNames =
    [
        "Identity", "Trips", "Drivers", "Fleet", "Clients",
        "Billing", "Incidents", "Notifications", "Grocery",
    ];

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

    public static string ModuleProjectPath(string module, string layer) =>
        Path.Combine(
            BackendRoot, "src", "Modules", module,
            $"NorthernLink.Modules.{module}.{layer}",
            $"NorthernLink.Modules.{module}.{layer}.csproj");

    /// <summary>Project names (file name without extension) referenced by a csproj.</summary>
    public static IReadOnlyList<string> ProjectReferences(string csprojPath) =>
        XDocument.Load(csprojPath)
            .Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .ToList();

    /// <summary>All modules discovered on disk — catches a module added without tests knowing.</summary>
    public static IReadOnlyList<string> ModulesOnDisk() =>
        Directory.GetDirectories(Path.Combine(BackendRoot, "src", "Modules"))
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();
}
