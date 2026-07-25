using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.Update;

public sealed class UpdateDriverCommandHandler(IDriverRepository repository)
    : ICommandHandler<UpdateDriverCommand>
{
    public async Task<Result> Handle(UpdateDriverCommand command, CancellationToken cancellationToken)
    {
        var driver = await repository.GetByIdAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure(DriverErrors.NotFound);
        }

        var result = driver.Update(
            command.Name,
            command.Phone,
            command.LicenceClass,
            command.LicenceExpiry,
            command.Source,
            command.HasWorkPermit);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
