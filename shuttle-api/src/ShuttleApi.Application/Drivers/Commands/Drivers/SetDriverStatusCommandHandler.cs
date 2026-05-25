using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

internal sealed class SetDriverStatusCommandHandler(IDriverRepository driverRepository)
    : IRequestHandler<SetDriverStatusCommand>
{
    public async Task Handle(SetDriverStatusCommand request, CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.Id} not found.");

        driver.SetStatus(request.Status);

        await driverRepository.UpdateAsync(driver, cancellationToken);
    }
}
