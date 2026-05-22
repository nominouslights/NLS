using Microsoft.Extensions.DependencyInjection;
using ShuttleApi.Application.Common.Behaviors;
using ShuttleApi.Application.Common.Mediator;

namespace ShuttleApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        return services;
    }
}
