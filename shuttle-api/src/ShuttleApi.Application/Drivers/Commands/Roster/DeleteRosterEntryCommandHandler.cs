using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Roster;

internal sealed class DeleteRosterEntryCommandHandler(IDriverRepository driverRepository)
    : IRequestHandler<DeleteRosterEntryCommand>
{
    public async Task Handle(DeleteRosterEntryCommand request, CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdWithRosterAsync(
            request.DriverId,
            DateOnly.MinValue,
            DateOnly.MaxValue,
            cancellationToken)
            ?? throw new NotFoundException($"Driver {request.DriverId} not found.");

        driver.RemoveRosterEntry(request.EntryId);
        await driverRepository.UpdateAsync(driver, cancellationToken);
    }
}
