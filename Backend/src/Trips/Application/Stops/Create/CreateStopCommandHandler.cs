using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Application.Stops.Create;

public sealed class CreateStopCommandHandler(IStopRepository repository)
    : ICommandHandler<CreateStopCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateStopCommand command, CancellationToken cancellationToken)
    {
        var addressResult = Address.Create(
            command.Street, command.City, command.Province, command.PostalCode, command.Country);
        if (addressResult.IsFailure)
        {
            return Result.Failure<Guid>(addressResult.Error);
        }

        var coordinateResult = Coordinate.Create(command.Latitude, command.Longitude);
        if (coordinateResult.IsFailure)
        {
            return Result.Failure<Guid>(coordinateResult.Error);
        }

        var stopResult = Stop.Create(
            command.TenantId,
            command.Name,
            command.Type,
            addressResult.Value,
            coordinateResult.Value,
            command.Notes);

        if (stopResult.IsFailure)
        {
            return Result.Failure<Guid>(stopResult.Error);
        }

        repository.Add(stopResult.Value);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(stopResult.Value.Id);
    }
}
