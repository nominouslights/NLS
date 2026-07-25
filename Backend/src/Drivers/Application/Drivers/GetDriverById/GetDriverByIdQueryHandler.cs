using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.GetDriverById;

public sealed class GetDriverByIdQueryHandler(IDriverReadService readService)
    : IQueryHandler<GetDriverByIdQuery, DriverResponse>
{
    public async Task<Result<DriverResponse>> Handle(
        GetDriverByIdQuery query,
        CancellationToken cancellationToken)
    {
        var driver = await readService.GetDriverAsync(query.DriverId, cancellationToken);
        return driver is null
            ? Result.Failure<DriverResponse>(DriverErrors.NotFound)
            : Result.Success(driver);
    }
}
