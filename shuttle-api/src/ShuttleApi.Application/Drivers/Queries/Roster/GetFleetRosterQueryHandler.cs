using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Queries.Roster;

internal sealed class GetFleetRosterQueryHandler(IDriverRepository driverRepository)
    : IRequestHandler<GetFleetRosterQuery, IReadOnlyList<DriverRosterSummaryResult>>
{
    public async Task<IReadOnlyList<DriverRosterSummaryResult>> Handle(
        GetFleetRosterQuery request,
        CancellationToken cancellationToken)
    {
        var driversWithRoster = await driverRepository.GetAllWithRosterAsync(
            request.RangeStart, request.RangeEnd, cancellationToken);

        return driversWithRoster.Select(tuple => new DriverRosterSummaryResult(
            tuple.Driver.Id,
            tuple.Driver.EmployeeId,
            tuple.Driver.FullName,
            tuple.Entries
                .Select(r => new RosterEntryResult(r.Id, r.EntryDate, r.Status.ToString(), r.ShiftStart, r.ShiftEnd))
                .ToList()
        )).ToList();
    }
}
