using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Clearances.GetForDriver;

public sealed class GetDriverClearancesQueryHandler(IDriverClearanceReadService readService)
    : IQueryHandler<GetDriverClearancesQuery, IReadOnlyList<DriverClearanceResponse>>
{
    public async Task<Result<IReadOnlyList<DriverClearanceResponse>>> Handle(
        GetDriverClearancesQuery query,
        CancellationToken cancellationToken)
    {
        var clearances = await readService.GetForDriverAsync(query.DriverId, cancellationToken);
        return Result.Success(clearances);
    }
}
