using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Clearances;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Clearances.Revoke;

public sealed class RevokeDriverClearanceCommandHandler(IDriverClearanceRepository repository)
    : ICommandHandler<RevokeDriverClearanceCommand>
{
    public async Task<Result> Handle(RevokeDriverClearanceCommand command, CancellationToken cancellationToken)
    {
        var clearance = await repository.GetByIdAsync(command.ClearanceId, cancellationToken);
        if (clearance is null)
        {
            return Result.Failure(ClearanceErrors.NotFound);
        }

        repository.Remove(clearance);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
