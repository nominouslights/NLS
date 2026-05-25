using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Roster;

internal sealed class UpsertRosterEntryCommandHandler(IDriverRepository driverRepository)
    : IRequestHandler<UpsertRosterEntryCommand, UpsertRosterEntryResult>
{
    public async Task<UpsertRosterEntryResult> Handle(
        UpsertRosterEntryCommand request,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.DriverId} not found.");

        // Check if an entry already exists for this driver/date
        var existingEntry = await driverRepository.GetRosterEntryAsync(
            request.DriverId, request.EntryDate, cancellationToken);

        Guid entryId;

        if (existingEntry is not null)
        {
            existingEntry.Update(request.Status, request.ShiftStart, request.ShiftEnd);
            entryId = existingEntry.Id;
        }
        else
        {
            var newEntry = DriverRosterEntry.Create(
                request.DriverId,
                request.EntryDate,
                request.Status,
                request.ShiftStart,
                request.ShiftEnd);

            driver.AddOrUpdateRosterEntry(newEntry);
            entryId = newEntry.Id;
        }

        await driverRepository.UpdateAsync(driver, cancellationToken);

        return new UpsertRosterEntryResult(entryId);
    }
}
