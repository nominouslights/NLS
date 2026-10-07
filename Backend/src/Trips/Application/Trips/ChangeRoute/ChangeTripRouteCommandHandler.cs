using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ChangeRoute;

public sealed class ChangeTripRouteCommandHandler(
    TripRouteChangeImpactCalculator calculator,
    ITripRepository tripRepository)
    : ICommandHandler<ChangeTripRouteCommand>
{
    public async Task<Result> Handle(ChangeTripRouteCommand command, CancellationToken cancellationToken)
    {
        var assessed = await calculator.CalculateAsync(command.TripId, command.RouteId, cancellationToken);
        if (assessed.IsFailure)
        {
            return Result.Failure(assessed.Error);
        }

        var impact = assessed.Value;
        if (impact.BlockingErrors.Count > 0)
        {
            return Result.Failure(impact.BlockingErrors[0]);
        }

        if (impact.RequiresAcknowledgement && !command.AcknowledgeWarnings)
        {
            return Result.Failure(TripErrors.RouteChangeNeedsAcknowledgement);
        }

        // No blockers means the route exists.
        var route = impact.Route!;
        foreach (var leg in impact.Legs.Where(leg => leg.WillChange))
        {
            var changed = leg.Trip.ChangeRoute(route);
            if (changed.IsFailure)
            {
                return changed;
            }

            leg.Manifest?.RenameRoute(route.Name);
        }

        // One DbContext behind every repository: both legs and their manifests commit together,
        // and a version-token loss on any of them comes back as a clean 409.
        return await tripRepository.TrySaveChangesAsync(cancellationToken)
            ? Result.Success()
            : Result.Failure(TripErrors.ChangedConcurrently);
    }
}
