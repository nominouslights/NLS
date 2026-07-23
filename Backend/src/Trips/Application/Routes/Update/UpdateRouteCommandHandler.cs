using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Routes.Update;

public sealed class UpdateRouteCommandHandler(IRouteRepository repository)
    : ICommandHandler<UpdateRouteCommand>
{
    public async Task<Result> Handle(UpdateRouteCommand command, CancellationToken cancellationToken)
    {
        var route = await repository.GetByIdAsync(command.RouteId, cancellationToken);
        if (route is null)
        {
            return Result.Failure(RouteErrors.NotFound);
        }

        var result = route.Update(
            command.Name,
            command.Stops,
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
