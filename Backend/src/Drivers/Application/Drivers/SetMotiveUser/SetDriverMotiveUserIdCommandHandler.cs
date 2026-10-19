using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.SetMotiveUser;

public sealed class SetDriverMotiveUserIdCommandHandler(IDriverRepository repository)
    : ICommandHandler<SetDriverMotiveUserIdCommand>
{
    public async Task<Result> Handle(SetDriverMotiveUserIdCommand command, CancellationToken cancellationToken)
    {
        var driver = await repository.GetByIdAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure(DriverErrors.NotFound);
        }

        var result = driver.SetMotiveUserId(command.MotiveUserId);
        if (result.IsFailure)
        {
            return result;
        }

        // Unique per tenant: two drivers sharing one Motive user would have one person's logs
        // mirrored onto both ledgers. The partial unique index enforces it too; checking here
        // turns the violation into a 409 instead of a 500. Write side (tenant-filtered), after
        // the aggregate has trimmed the value.
        if (driver.MotiveUserId is { } motiveUserId)
        {
            var existing = await repository.GetByMotiveUserIdAsync(motiveUserId, cancellationToken);
            if (existing is not null && existing.Id != driver.Id)
            {
                return Result.Failure(DriverErrors.DuplicateMotiveUserId);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
