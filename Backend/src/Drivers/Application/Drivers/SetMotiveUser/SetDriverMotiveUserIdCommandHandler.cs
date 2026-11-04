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
        // mirrored onto both ledgers. Write side (tenant-filtered), after the aggregate has
        // trimmed the value.
        if (driver.MotiveUserId is { } motiveUserId
            && await repository.ExistsByMotiveUserIdAsync(motiveUserId, driver.Id, cancellationToken))
        {
            return Result.Failure(DriverErrors.DuplicateMotiveUserId);
        }

        // The check and the save are not atomic: two concurrent mappings can both pass it. The
        // partial unique index then refuses the loser, and the repository translates that one
        // constraint into the same 409 rather than letting it surface as a 500.
        return await repository.TrySaveMotiveUserIdAsync(cancellationToken)
            ? Result.Success()
            : Result.Failure(DriverErrors.DuplicateMotiveUserId);
    }
}
