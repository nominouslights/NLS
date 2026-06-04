using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

internal sealed class RestoreDriverCommandHandler(IDriverRepository driverRepository)
    : IRequestHandler<RestoreDriverCommand>
{
    public async Task Handle(RestoreDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetDeletedByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException($"Archived driver {request.DriverId} not found.");

        driver.Restore();

        await driverRepository.UpdateAsync(driver, cancellationToken);
    }
}
