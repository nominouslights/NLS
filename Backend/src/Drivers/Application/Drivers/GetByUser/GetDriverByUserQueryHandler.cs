using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.GetByUser;

public sealed class GetDriverByUserQueryHandler(IDriverReadService readService)
    : IQueryHandler<GetDriverByUserQuery, DriverResponse>
{
    public async Task<Result<DriverResponse>> Handle(
        GetDriverByUserQuery query, CancellationToken cancellationToken)
    {
        var driver = await readService.GetDriverByUserAsync(query.UserId, cancellationToken);

        // Drivers.NotLinked, not the generic Drivers.Driver.NotFound: the Field App's very first
        // call is this one, and "no driver row for your account" is an onboarding state with a
        // specific fix ("ask dispatch to link you"), not a lookup miss. The app branches on this
        // exact code, so it is pinned by a test.
        return driver is null
            ? Result.Failure<DriverResponse>(DriverErrors.NotLinked)
            : Result.Success(driver);
    }
}
