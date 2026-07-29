using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Routes;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Routes.Update;

public sealed class UpdateRouteCommandHandler(IRouteRepository repository, IStopRepository stops)
    : ICommandHandler<UpdateRouteCommand>
{
    public async Task<Result> Handle(UpdateRouteCommand command, CancellationToken cancellationToken)
    {
        var route = await repository.GetByIdAsync(command.RouteId, cancellationToken);
        if (route is null)
        {
            return Result.Failure(RouteErrors.NotFound);
        }

        var stopsResult = await RouteStopResolver.ResolveAsync(stops, command.StopIds, cancellationToken);
        if (stopsResult.IsFailure)
        {
            return stopsResult;
        }

        var result = route.Update(
            command.Name,
            stopsResult.Value,
            command.DistanceKm,
            TimeSpan.FromMinutes(command.EstimatedDurationMinutes),
            command.RequiredLicenceClass,
            command.Active);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
