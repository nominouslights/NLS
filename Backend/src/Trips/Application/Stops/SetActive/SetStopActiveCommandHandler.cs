using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Application.Stops.SetActive;

public sealed class SetStopActiveCommandHandler(IStopRepository repository)
    : ICommandHandler<SetStopActiveCommand>
{
    public async Task<Result> Handle(SetStopActiveCommand command, CancellationToken cancellationToken)
    {
        var stop = await repository.GetByIdAsync(command.StopId, cancellationToken);
        if (stop is null)
        {
            return Result.Failure(StopErrors.NotFound);
        }

        stop.SetActive(command.Active);

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
