using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Routes.Create;

public sealed class CreateRouteCommandHandler(IRouteRepository repository)
    : ICommandHandler<CreateRouteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRouteCommand command, CancellationToken cancellationToken)
    {
        var routeResult = Route.Create(
            command.TenantId,
            command.Name,
            command.Stops,
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
