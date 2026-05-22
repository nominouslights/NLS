using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ShuttleApi.Application.Common.Mediator;

public sealed class MediatorConfiguration(IServiceCollection services)
{
    public MediatorConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        var handlerDef = typeof(IRequestHandler<,>);
        var voidHandlerDef = typeof(IRequestHandler<>);

        foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var def = iface.GetGenericTypeDefinition();

                if (def == handlerDef)
                {
                    services.TryAddTransient(iface, type);
                }
                else if (def == voidHandlerDef)
                {
                    services.TryAddTransient(iface, type);

                    var tRequest = iface.GetGenericArguments()[0];
                    var typedService = typeof(IRequestHandler<,>).MakeGenericType(tRequest, typeof(Unit));
                    var wrapperType = typeof(VoidHandlerWrapper<>).MakeGenericType(tRequest);
                    services.TryAddTransient(typedService, wrapperType);
                }
            }
        }

        return this;
    }

    public MediatorConfiguration AddBehavior(Type behaviorInterface, Type behaviorImpl)
    {
        services.AddTransient(behaviorInterface, behaviorImpl);
        return this;
    }
}

internal sealed class VoidHandlerWrapper<TRequest>(IRequestHandler<TRequest> inner)
    : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest
{
    public async Task<Unit> Handle(TRequest request, CancellationToken cancellationToken)
    {
        await inner.Handle(request, cancellationToken);
        return Unit.Value;
    }
}

public static class MediatorExtensions
{
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Action<MediatorConfiguration> configure)
    {
        services.AddTransient<ISender, Mediator>();
        configure(new MediatorConfiguration(services));
        return services;
    }
}
