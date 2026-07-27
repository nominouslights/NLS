using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.Create;

public sealed class CreateTripManifestCommandHandler(ITripManifestRepository repository)
    : ICommandHandler<CreateTripManifestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTripManifestCommand command, CancellationToken cancellationToken)
    {
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
