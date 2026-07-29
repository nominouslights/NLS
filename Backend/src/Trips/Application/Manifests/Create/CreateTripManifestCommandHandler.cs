using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.Create;

public sealed class CreateTripManifestCommandHandler(
    ITripManifestRepository repository,
    ITripRepository tripRepository)
    : ICommandHandler<CreateTripManifestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTripManifestCommand command, CancellationToken cancellationToken)
    {
        // Deadheads carry no passengers by definition — refuse a manifest outright rather
        // than letting the async attach link one later. A manifest whose trip number
        // matches no trip yet is still allowed (manifests associate by number, lazily).
        var trip = await tripRepository.GetByTripNumberAsync(command.TripNumber, cancellationToken);
        if (trip is { IsEmptyLeg: true })
        {
            return Result.Failure<Guid>(Domain.Trips.TripErrors.ManifestNotAllowedForEmptyLeg);
        }

        var manifestResult = TripManifest.Create(
            command.TenantId,
            command.TripDate,
            command.TripNumber,
            command.Route,
            command.Direction,
            command.Client,
            command.Passengers,
            command.AllSeatbeltsVerified,
            command.Cargo,
            command.AllCargoSecured,
            command.Source,
            command.EnteredBy);

        if (manifestResult.IsFailure)
        {
            return Result.Failure<Guid>(manifestResult.Error);
        }

        repository.Add(manifestResult.Value);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(manifestResult.Value.Id);
    }
}
