using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Queries.Roster;

internal sealed class GetDriverRosterQueryHandler(IDriverRepository driverRepository)
    : IRequestHandler<GetDriverRosterQuery, IReadOnlyList<RosterEntryResult>>
{
    public async Task<IReadOnlyList<RosterEntryResult>> Handle(
        GetDriverRosterQuery request,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdWithRosterAsync(
            request.DriverId,
            request.RangeStart,
            request.RangeEnd,
            cancellationToken)
            ?? throw new NotFoundException($"Driver {request.DriverId} not found.");

        return driver.RosterEntries
            .Select(r => new RosterEntryResult(r.Id, r.EntryDate, r.Status.ToString(), r.ShiftStart, r.ShiftEnd))
            .ToList();
    }
}
