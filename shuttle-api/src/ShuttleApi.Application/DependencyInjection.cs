using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ShuttleApi.Application.Common.Behaviors;

namespace ShuttleApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        return services;
    }
}
