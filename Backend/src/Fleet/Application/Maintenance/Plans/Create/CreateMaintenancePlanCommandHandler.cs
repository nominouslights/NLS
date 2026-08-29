using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.Create;

public sealed class CreateMaintenancePlanCommandHandler(IMaintenancePlanRepository repository)
    : ICommandHandler<CreateMaintenancePlanCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateMaintenancePlanCommand command, CancellationToken cancellationToken)
    {
        // Application-level duplicate probe so a taken name is a domain conflict, not a
        // 23505 bubbling up as a 500; the unique (tenant_id, name) index stays the backstop.
        // Stored names are trimmed by the aggregate, so probe with the trimmed name too.
        if (await repository.FindIdByNameAsync(command.Name.Trim(), cancellationToken) is not null)
        {
            return Result.Failure<Guid>(MaintenanceErrors.NameTaken);
        }

        var planResult = MaintenancePlan.Create(
            command.TenantId,
            command.Name,
            command.VehicleModel,
            command.ServiceClass,
            command.Notes,
            command.Items,
            command.Overhauls);

        if (planResult.IsFailure)
        {
            return Result.Failure<Guid>(planResult.Error);
        }

        repository.Add(planResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(planResult.Value.Id);
    }
}
