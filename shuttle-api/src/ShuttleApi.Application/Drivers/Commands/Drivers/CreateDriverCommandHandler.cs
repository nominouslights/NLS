using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

internal sealed class CreateDriverCommandHandler(IDriverRepository driverRepository)
    : IRequestHandler<CreateDriverCommand, CreateDriverResult>
{
    public async Task<CreateDriverResult> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        // Enforce unique EmployeeId
        var existing = await driverRepository.GetAllAsync(cancellationToken);
        if (existing.Any(d => d.EmployeeId.Equals(request.EmployeeId, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"A driver with employee ID '{request.EmployeeId}' already exists.");

        var driver = Driver.Create(
            request.EmployeeId,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.Email,
            DateTime.SpecifyKind(request.HireDate, DateTimeKind.Utc));

        await driverRepository.AddAsync(driver, cancellationToken);

        return new CreateDriverResult(driver.Id);
    }
}
