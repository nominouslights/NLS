using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.Register;

public sealed class RegisterDriverCommandHandler(IDriverRepository repository)
    : ICommandHandler<RegisterDriverCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterDriverCommand command, CancellationToken cancellationToken)
    {
        var driverResult = Driver.Register(
            command.TenantId,
            command.Name,
            command.Phone,
            command.LicenceClass,
            command.LicenceExpiry,
            command.Source,
            command.HasWorkPermit);

        if (driverResult.IsFailure)
        {
            return Result.Failure<Guid>(driverResult.Error);
        }

        repository.Add(driverResult.Value);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(driverResult.Value.Id);
    }
}
