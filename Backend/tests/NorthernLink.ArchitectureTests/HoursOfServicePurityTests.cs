using System.Text.RegularExpressions;
using NetArchTest.Rules;
using NorthernLink.Shared.HoursOfService;
using Xunit;

namespace NorthernLink.ArchitectureTests;

/// <summary>
/// The HOS rule engine (<c>NorthernLink.Shared.HoursOfService</c>) is the one piece of business
/// logic that lives in Shared, because Drivers and Trips must agree on every boundary bit for bit
/// and the TypeScript mirror must be able to reproduce it. The price of that exception is purity:
/// System-only, no clock, no I/O, no randomness, no async. These tests are the guard.
/// </summary>
public class HoursOfServicePurityTests
{
    private const string EngineNamespace = "NorthernLink.Shared.HoursOfService";

    private static readonly string EngineSourceRoot =
        Path.Combine(ModuleGraph.BackendRoot, "src", "Shared", "HoursOfService");

    [Fact]
    public void Engine_depends_on_no_other_NorthernLink_namespace_and_no_infrastructure()
    {
        var shared = typeof(HosEngine).Assembly;

        // Every other namespace in Shared (Kernel included — the engine returns HosLedgerProblems,
        // not Kernel Errors), plus every domain library, plus the infrastructure packages.
        // NetArchTest matches by prefix, so a namespace that is itself a prefix of the engine's
        // (the root NorthernLink.Shared, home of SharedServiceCollectionExtensions) would match
        // the engine; those are listed by full type name instead.
        var namespaces = shared.GetTypes()
            .Select(t => t.Namespace)
            .Where(ns => ns is not null && ns.StartsWith("NorthernLink.", StringComparison.Ordinal))
            .Select(ns => ns!)
            .Distinct()
            .ToList();

        var forbidden = namespaces
            .Where(ns => ns != EngineNamespace
                && !ns.StartsWith(EngineNamespace + ".", StringComparison.Ordinal)
                && !EngineNamespace.StartsWith(ns + ".", StringComparison.Ordinal))
            .Concat(shared.GetTypes()
                .Where(t => t.Namespace is not null && EngineNamespace.StartsWith(t.Namespace + ".", StringComparison.Ordinal))
                .Select(t => t.FullName!))
            .Concat(ModuleGraph.DomainNames.Select(d => $"NorthernLink.{d}"))
            .Concat(["Microsoft", "Npgsql", "RabbitMQ", "Amazon"])
            .Distinct()
            .ToArray();

        Assert.Contains("NorthernLink.Shared.Kernel", forbidden);

        var result = Types.InAssembly(shared)
            .That().ResideInNamespaceStartingWith(EngineNamespace)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "The HOS engine must be System-only. Offending types: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Engine_source_folder_exists_where_the_scan_expects_it()
    {
        Assert.True(Directory.Exists(EngineSourceRoot), $"Missing {EngineSourceRoot}");
        Assert.NotEmpty(Directory.GetFiles(EngineSourceRoot, "*.cs", SearchOption.AllDirectories));
    }

    public static TheoryData<string, string> ForbiddenSourcePatterns() => new()
    {
        { "clock", @"\b(DateTime|DateTimeOffset)\.(UtcNow|Now)\b" },
        { "local time zone", @"TimeZoneInfo\.Local" },
        { "randomness", @"\bRandom\b" },
        { "environment", @"Environment\." },
        { "file system", @"System\.IO" },
        { "Task", @"\bTask\b" },
        { "async", @"\basync\b" },
    };

    /// <summary>
    /// A source scan complements the IL test: a clock read or a <c>Task</c> is a System type the
    /// dependency rule above cannot see, so the text itself is checked.
    /// </summary>
    [Theory]
    [MemberData(nameof(ForbiddenSourcePatterns))]
    public void Engine_source_never_mentions(string what, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        var offenders = Directory.GetFiles(EngineSourceRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (file, line, index))
                .Where(x => regex.IsMatch(x.line))
                .Select(x => $"{Path.GetFileName(x.file)}:{x.index + 1}: {x.line.Trim()}"))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"The HOS engine must not use {what} (pattern {pattern}):\n{string.Join("\n", offenders)}");
    }
}
