using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.LinkUser;

public sealed class LinkDriverUserCommandHandler(IDriverRepository repository)
    : ICommandHandler<LinkDriverUserCommand>
{
    public async Task<Result> Handle(LinkDriverUserCommand command, CancellationToken cancellationToken)
    {
        var driver = await repository.GetByIdAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure(DriverErrors.NotFound);
        }

        // One account, one driver. The partial unique index on user_id enforces this in the
        // database too, but a raw unique violation surfaces as a 500 with a Postgres string in
        // it; checking here turns the same condition into a 409 the console can explain. Write
        // side, so a link made seconds ago is already visible (rm_drivers would lag a poll).
        var existing = await repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (existing is not null && existing.Id != command.DriverId)
        {
            return Result.Failure(DriverErrors.UserAlreadyLinkedToAnotherDriver);
        }

        var result = driver.LinkUser(command.UserId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
