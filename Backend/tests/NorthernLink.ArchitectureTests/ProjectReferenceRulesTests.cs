using Xunit;

namespace NorthernLink.ArchitectureTests;

/// <summary>
/// The microservice-mimicry rules, enforced on the .csproj reference graph:
///
///   Shared          → nothing (internal)
///   Domain library  → NorthernLink.Shared only — never another domain library
///   Api gateway     → NorthernLink.Shared + domain libraries only
///
/// Cross-domain communication is integration-events-only over the bus; there is no
/// compile-time path from one domain library to another, so extracting a library into
/// its own microservice is a copy-out of that library plus Shared.
/// </summary>
public class ProjectReferenceRulesTests
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

    [Fact]
    public void Every_domain_library_on_disk_is_covered_by_these_tests()
    {
        var onDisk = ModuleGraph.DomainsOnDisk();
        var known = ModuleGraph.DomainNames.OrderBy(name => name).ToList();

        Assert.Equal(known, onDisk);
    }

    [Fact]
    public void Shared_references_no_projects()
    {
        var references = ModuleGraph.ProjectReferences(ModuleGraph.SharedProjectPath);

        Assert.Empty(references);
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Domain_libraries_reference_shared_only(string domain)
    {
        var references = ModuleGraph.ProjectReferences(ModuleGraph.DomainProjectPath(domain));

        var illegal = references.Where(r => r != "NorthernLink.Shared").ToList();
        Assert.True(illegal.Count == 0,
            $"NorthernLink.{domain} has illegal references: {string.Join(", ", illegal)}. " +
            "A domain library may reference NorthernLink.Shared only — cross-domain " +
            "communication is integration-events-only.");
    }

    [Fact]
    public void Api_gateway_references_shared_and_domain_libraries_only()
    {
        var references = ModuleGraph.ProjectReferences(ModuleGraph.ApiProjectPath);

        var allowed = ModuleGraph.DomainNames
            .Select(domain => $"NorthernLink.{domain}")
            .Append("NorthernLink.Shared")
            .ToHashSet();

        var illegal = references.Where(r => !allowed.Contains(r)).ToList();
        Assert.True(illegal.Count == 0,
            $"The API gateway has illegal references: {string.Join(", ", illegal)}. " +
            "It may only reference NorthernLink.Shared and the domain libraries.");
    }
}
