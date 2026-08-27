using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Routes.Create;

public sealed class CreateRouteCommandHandler(IRouteRepository repository, IStopRepository stops)
    : ICommandHandler<CreateRouteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRouteCommand command, CancellationToken cancellationToken)
    {
        var stopsResult = await RouteStopResolver.ResolveAsync(stops, command.Stops, cancellationToken);
        if (stopsResult.IsFailure)
        {
            return Result.Failure<Guid>(stopsResult.Error);
        }

        var routeResult = Route.Create(
            command.TenantId,
            command.Name,
            stopsResult.Value,
            command.DistanceKm,
            TimeSpan.FromMinutes(command.EstimatedDurationMinutes),
            command.RequiredLicenceClass);

        if (routeResult.IsFailure)
        {
            return Result.Failure<Guid>(routeResult.Error);
        }

        repository.Add(routeResult.Value);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(routeResult.Value.Id);
    }
}
