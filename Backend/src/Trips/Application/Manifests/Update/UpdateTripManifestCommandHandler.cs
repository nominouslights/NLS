using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.Update;

public sealed class UpdateTripManifestCommandHandler(ITripManifestRepository repository)
    : ICommandHandler<UpdateTripManifestCommand>
{
    public async Task<Result> Handle(UpdateTripManifestCommand command, CancellationToken cancellationToken)
    {
        var manifest = await repository.GetByIdAsync(command.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return Result.Failure(TripManifestErrors.NotFound);
        }

        var result = manifest.Update(
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

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
