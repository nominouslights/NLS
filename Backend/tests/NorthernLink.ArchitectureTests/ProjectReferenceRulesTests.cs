using Xunit;

namespace NorthernLink.ArchitectureTests;

/// <summary>
/// The microservice-mimicry rules, enforced on the .csproj reference graph:
///
///   Contracts      → nothing
///   Domain         → SharedKernel only
///   Application    → own Domain, own Contracts, BuildingBlocks.Application, other modules' CONTRACTS only
///   Infrastructure → own Application, own Contracts, BuildingBlocks.Infrastructure
///   Api host       → BuildingBlocks.Infrastructure + module INFRASTRUCTURE projects only
/// </summary>
public class ProjectReferenceRulesTests
{
    public static TheoryData<string> Modules()
    {
        var data = new TheoryData<string>();
        foreach (var module in ModuleGraph.ModuleNames)
        {
            data.Add(module);
        }

        return data;
    }

    [Fact]
    public void Every_module_on_disk_is_covered_by_these_tests()
    {
        var onDisk = ModuleGraph.ModulesOnDisk();
        var known = ModuleGraph.ModuleNames.OrderBy(name => name).ToList();

        Assert.Equal(known, onDisk);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Contracts_projects_reference_nothing(string module)
    {
        var references = ModuleGraph.ProjectReferences(ModuleGraph.ModuleProjectPath(module, "Contracts"));

        Assert.Empty(references);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_projects_reference_shared_kernel_only(string module)
    {
        var references = ModuleGraph.ProjectReferences(ModuleGraph.ModuleProjectPath(module, "Domain"));

        var illegal = references.Where(r => r != "NorthernLink.SharedKernel").ToList();
        Assert.True(illegal.Count == 0,
            $"{module}.Domain has illegal references: {string.Join(", ", illegal)}. Domain may reference SharedKernel only.");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_projects_reference_own_layers_and_other_contracts_only(string module)
    {
        var references = ModuleGraph.ProjectReferences(ModuleGraph.ModuleProjectPath(module, "Application"));

        var illegal = references.Where(r => !IsAllowedForApplication(module, r)).ToList();
        Assert.True(illegal.Count == 0,
            $"{module}.Application has illegal references: {string.Join(", ", illegal)}. " +
            "Another module may only be referenced through its Contracts project.");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Infrastructure_projects_reference_own_application_and_building_blocks_only(string module)
    {
        var references = ModuleGraph.ProjectReferences(ModuleGraph.ModuleProjectPath(module, "Infrastructure"));

        var allowed = new[]
        {
            $"NorthernLink.Modules.{module}.Application",
            $"NorthernLink.Modules.{module}.Contracts",
            "NorthernLink.BuildingBlocks.Infrastructure",
        };

        var illegal = references.Where(r => !allowed.Contains(r)).ToList();
        Assert.True(illegal.Count == 0,
            $"{module}.Infrastructure has illegal references: {string.Join(", ", illegal)}.");
    }

    [Fact]
    public void Api_host_references_module_infrastructure_and_building_blocks_only()
    {
        var apiProject = Path.Combine(
            ModuleGraph.BackendRoot, "src", "Api", "NorthernLink.Api", "NorthernLink.Api.csproj");
        var references = ModuleGraph.ProjectReferences(apiProject);

        var illegal = references.Where(r =>
                r != "NorthernLink.BuildingBlocks.Infrastructure" &&
                !(r.StartsWith("NorthernLink.Modules.", StringComparison.Ordinal) &&
                  r.EndsWith(".Infrastructure", StringComparison.Ordinal)))
            .ToList();

        Assert.True(illegal.Count == 0,
            $"The API host has illegal references: {string.Join(", ", illegal)}. " +
            "It may only reference BuildingBlocks.Infrastructure and module Infrastructure projects.");
    }

    private static bool IsAllowedForApplication(string module, string reference)
    {
        if (reference == $"NorthernLink.Modules.{module}.Domain" ||
            reference == $"NorthernLink.Modules.{module}.Contracts" ||
            reference == "NorthernLink.BuildingBlocks.Application")
        {
            return true;
        }

        // Other modules: Contracts only.
        return reference.StartsWith("NorthernLink.Modules.", StringComparison.Ordinal) &&
               reference.EndsWith(".Contracts", StringComparison.Ordinal);
    }
}
