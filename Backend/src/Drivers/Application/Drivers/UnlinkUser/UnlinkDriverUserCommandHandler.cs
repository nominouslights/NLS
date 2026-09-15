using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.UnlinkUser;

public sealed class UnlinkDriverUserCommandHandler(IDriverRepository repository)
    : ICommandHandler<UnlinkDriverUserCommand>
{
    public async Task<Result> Handle(UnlinkDriverUserCommand command, CancellationToken cancellationToken)
    {
        var driver = await repository.GetByIdAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure(DriverErrors.NotFound);
        }

        var result = driver.UnlinkUser();
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
