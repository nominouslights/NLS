using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Clearances;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Clearances.Grant;

public sealed class GrantDriverClearanceCommandHandler(IDriverClearanceRepository repository)
    : ICommandHandler<GrantDriverClearanceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(GrantDriverClearanceCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.DriverExistsAsync(command.DriverId, cancellationToken))
        {
            return Result.Failure<Guid>(DriverErrors.NotFound);
        }

        var clearanceResult = DriverClearance.Grant(
            command.TenantId,
            command.DriverId,
            command.Title,
            command.ClientName,
            command.Expiry);

        if (clearanceResult.IsFailure)
        {
            return Result.Failure<Guid>(clearanceResult.Error);
        }

        repository.Add(clearanceResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(clearanceResult.Value.Id);
    }
}
