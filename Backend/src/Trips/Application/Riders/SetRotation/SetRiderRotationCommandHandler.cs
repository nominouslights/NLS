using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Riders;

namespace NorthernLink.Trips.Application.Riders.SetRotation;

public sealed class SetRiderRotationCommandHandler(IRiderRepository repository)
    : ICommandHandler<SetRiderRotationCommand>
{
    public async Task<Result> Handle(SetRiderRotationCommand command, CancellationToken cancellationToken)
    {
        var rider = await repository.GetByIdAsync(command.RiderId, cancellationToken);
        if (rider is null)
        {
            return Result.Failure(RiderErrors.NotFound);
        }

        var result = rider.SetRotation(command.RotationDays);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
