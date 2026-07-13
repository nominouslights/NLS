using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace NorthernLink.ArchitectureTests;

/// <summary>
/// IL-level dependency rules via NetArchTest. These complement the csproj-graph tests:
/// with each domain now a single assembly, the layer boundaries (Domain / Application /
/// Infrastructure) live in namespaces, so only IL rules can enforce them. They also catch
/// cross-domain type leaks smuggled through transitive dependencies.
/// </summary>
public class TypeDependencyRulesTests
{
    public static TheoryData<string> Domains()
    {
        var data = new TheoryData<string>();
        foreach (var domain in ModuleGraph.DomainNames)
        {
            data.Add(domain);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Domain_libraries_do_not_depend_on_other_domain_libraries(string domain)
    {
        var forbiddenNamespaces = ModuleGraph.DomainNames
            .Where(other => other != domain)
            .Select(other => $"NorthernLink.{other}")
            .ToArray();

        var assembly = LoadDomainAssembly(domain);
        var result = Types.InAssembly(assembly)
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NorthernLink.{domain} has types depending on another domain library: " +
            $"{string.Join(", ", result.FailingTypeNames ?? [])}. " +
            "Cross-domain communication is integration-events-only.");
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Domain_layer_stays_free_of_upper_layers_and_infrastructure(string domain)
    {
        var assembly = LoadDomainAssembly(domain);

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith($"NorthernLink.{domain}.Domain")
            .ShouldNot().HaveDependencyOnAny(
                $"NorthernLink.{domain}.Application",
                $"NorthernLink.{domain}.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "RabbitMQ.Client",
                // Domain may use NorthernLink.Shared.Kernel only.
                "NorthernLink.Shared.Messaging",
                "NorthernLink.Shared.Events",
                "NorthernLink.Shared.EventBus",
                "NorthernLink.Shared.Persistence",
                "NorthernLink.Shared.Tenancy")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NorthernLink.{domain}'s Domain layer has illegal dependencies: " +
            $"{string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Application_layer_does_not_depend_on_infrastructure(string domain)
    {
        var assembly = LoadDomainAssembly(domain);

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith($"NorthernLink.{domain}.Application")
            .ShouldNot().HaveDependencyOnAny(
                $"NorthernLink.{domain}.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "RabbitMQ.Client")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"NorthernLink.{domain}'s Application layer has illegal dependencies: " +
            $"{string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    private static Assembly LoadDomainAssembly(string domain) =>
        Assembly.Load($"NorthernLink.{domain}");
}
