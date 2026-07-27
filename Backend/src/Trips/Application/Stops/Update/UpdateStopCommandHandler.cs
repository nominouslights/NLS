using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Application.Stops.Update;

public sealed class UpdateStopCommandHandler(IStopRepository repository)
    : ICommandHandler<UpdateStopCommand>
{
    public async Task<Result> Handle(UpdateStopCommand command, CancellationToken cancellationToken)
    {
        var stop = await repository.GetByIdAsync(command.StopId, cancellationToken);
        if (stop is null)
        {
            return Result.Failure(StopErrors.NotFound);
        }

        var addressResult = Address.Create(
            command.Street, command.City, command.Province, command.PostalCode, command.Country);
        if (addressResult.IsFailure)
        {
            return addressResult;
        }

        var coordinateResult = Coordinate.Create(command.Latitude, command.Longitude);
        if (coordinateResult.IsFailure)
        {
            return coordinateResult;
        }

        var result = stop.Update(
            command.Name,
            command.Type,
            addressResult.Value,
            coordinateResult.Value,
            command.Notes,
            command.Active);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
