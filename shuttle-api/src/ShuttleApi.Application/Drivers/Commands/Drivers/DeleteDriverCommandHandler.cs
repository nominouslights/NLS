using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

internal sealed class DeleteDriverCommandHandler(IDriverRepository driverRepository)
    : IRequestHandler<DeleteDriverCommand>
{
    public async Task Handle(DeleteDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.Id} not found.");

        await driverRepository.DeleteAsync(driver, cancellationToken);
    }
}
