using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Shared.Messaging;
using Xunit;

namespace NorthernLink.ArchitectureTests;

/// <summary>
/// Guards the two runtime failure modes of the reflection-based <c>Sender</c>
/// (Shared/Messaging/Sender.cs), both of which only surface when a request actually
/// dispatches the command:
/// 1. an <c>internal</c> handler → <c>RuntimeBinderException</c> from the <c>dynamic</c>
///    dispatch, because the binder honors accessibility;
/// 2. a handler that was written but never registered in the module's
///    <c>Add{Domain}</c> extension → <c>InvalidOperationException</c> from
///    <c>GetRequiredService</c>.
/// Scaffold modules (no handlers, no registrations) pass vacuously.
/// </summary>
public class HandlerRegistrationRulesTests
{
    private static readonly Type[] HandlerInterfaceDefinitions =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

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
    public void Handler_implementations_are_public(string domain)
    {
        var offenders = HandlerImplementations(LoadDomainAssembly(domain))
            .Where(type => !type.IsVisible)
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"NorthernLink.{domain} has non-public command/query handlers: " +
            $"{string.Join(", ", offenders)}. The Sender dispatches via `dynamic`, and the " +
            "runtime binder honors accessibility — an internal handler compiles and registers " +
            "fine, then throws RuntimeBinderException on the first real dispatch. Make the " +
            "handler public.");
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Every_handler_is_registered_in_the_module_di_extension_and_vice_versa(string domain)
    {
        var assembly = LoadDomainAssembly(domain);

        var implemented = HandlerImplementations(assembly)
            .SelectMany(HandlerInterfacesOf)
            .ToHashSet();

        // Invoke the module's real Add{Domain}(services, configuration) by reflection and
        // inspect the ServiceDescriptors. The provider is deliberately never built: every
        // module defers its environment lookups (connection strings, …) into factory
        // lambdas, so registration itself needs no environment.
        var services = new ServiceCollection();
        var extensionsType = assembly.GetType(
            $"NorthernLink.{domain}.Infrastructure.{domain}ServiceCollectionExtensions");
        Assert.True(extensionsType is not null,
            $"NorthernLink.{domain} has no {domain}ServiceCollectionExtensions — every domain " +
            "library exposes exactly one DI entry point for the gateway.");

        var addMethod = extensionsType!.GetMethod($"Add{domain}", BindingFlags.Public | BindingFlags.Static);
        Assert.True(addMethod is not null,
            $"{extensionsType.Name} has no public static Add{domain} method.");

        addMethod!.Invoke(null, [services, new ConfigurationBuilder().Build()]);

        var registered = services
            .Select(descriptor => descriptor.ServiceType)
            .Where(IsClosedHandlerInterface)
            .ToHashSet();

        var unregistered = implemented.Except(registered).Select(TypeName).Order().ToList();
        Assert.True(unregistered.Count == 0,
            $"NorthernLink.{domain} implements handlers that Add{domain} never registers: " +
            $"{string.Join(", ", unregistered)}. The Sender resolves handlers from DI (no " +
            "assembly scanning) — an unregistered handler throws InvalidOperationException on " +
            "the first dispatch. Add the missing AddScoped line.");

        var unimplemented = registered.Except(implemented).Select(TypeName).Order().ToList();
        Assert.True(unimplemented.Count == 0,
            $"NorthernLink.{domain}'s Add{domain} registers handler interfaces no class in the " +
            $"assembly implements: {string.Join(", ", unimplemented)}. Stale or wrong-module " +
            "registration — remove or fix it.");
    }

    /// <summary>Non-abstract classes in the assembly implementing any closed handler interface.</summary>
    private static List<Type> HandlerImplementations(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => HandlerInterfacesOf(type).Any())
            .ToList();

    private static IEnumerable<Type> HandlerInterfacesOf(Type type) =>
        type.GetInterfaces().Where(IsClosedHandlerInterface);

    private static bool IsClosedHandlerInterface(Type type) =>
        type.IsGenericType
        && !type.ContainsGenericParameters
        && HandlerInterfaceDefinitions.Contains(type.GetGenericTypeDefinition());

    private static string TypeName(Type type) =>
        $"{type.Name}<{string.Join(", ", type.GetGenericArguments().Select(a => a.Name))}>";

    private static Assembly LoadDomainAssembly(string domain) =>
        Assembly.Load($"NorthernLink.{domain}");
}
