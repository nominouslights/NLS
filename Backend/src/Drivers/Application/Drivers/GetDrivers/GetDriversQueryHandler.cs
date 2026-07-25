using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.GetDrivers;

public sealed class GetDriversQueryHandler(IDriverReadService readService)
    : IQueryHandler<GetDriversQuery, IReadOnlyList<DriverResponse>>
{
    public async Task<Result<IReadOnlyList<DriverResponse>>> Handle(
        GetDriversQuery query,
        CancellationToken cancellationToken)
    {
        var drivers = await readService.GetDriversAsync(cancellationToken);
        return Result.Success(drivers);
    }
}
