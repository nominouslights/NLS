using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

internal sealed class UpdateDriverCommandHandler(IDriverRepository driverRepository)
    : IRequestHandler<UpdateDriverCommand>
{
    public async Task Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.Id} not found.");

        driver.Update(
            request.EmployeeId,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.Email,
            request.HireDate,
            request.IsActive);

        await driverRepository.UpdateAsync(driver, cancellationToken);
    }
}
