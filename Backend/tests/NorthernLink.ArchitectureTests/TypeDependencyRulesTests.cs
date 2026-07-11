using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace NorthernLink.ArchitectureTests;

/// <summary>
/// IL-level dependency rules via NetArchTest. These complement the csproj-graph tests:
/// while modules are empty scaffolds they pass vacuously, but as real code lands they
/// catch type-level leaks the reference graph can't see (e.g. a type smuggled through
/// a transitive dependency).
/// </summary>
public class TypeDependencyRulesTests
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

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_types_do_not_depend_on_other_modules_internals(string module)
    {
        var internalLayers = new[] { "Domain", "Application", "Infrastructure" };

        var forbiddenNamespaces = ModuleGraph.ModuleNames
            .Where(other => other != module)
            .SelectMany(other => internalLayers.Select(layer => $"NorthernLink.Modules.{other}.{layer}"))
            .ToArray();

        foreach (var layer in internalLayers)
        {
            var assembly = LoadModuleAssembly(module, layer);
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"{module}.{layer} has types depending on another module's internals: " +
                $"{string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_types_stay_free_of_infrastructure_concerns(string module)
    {
        var assembly = LoadModuleAssembly(module, "Domain");

        var result = Types.InAssembly(assembly)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "RabbitMQ.Client",
                "NorthernLink.BuildingBlocks.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{module}.Domain has types depending on infrastructure: " +
            $"{string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    private static Assembly LoadModuleAssembly(string module, string layer) =>
        Assembly.Load($"NorthernLink.Modules.{module}.{layer}");
}
