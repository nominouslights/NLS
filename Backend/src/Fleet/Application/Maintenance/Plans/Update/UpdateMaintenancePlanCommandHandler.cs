using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.Update;

public sealed class UpdateMaintenancePlanCommandHandler(IMaintenancePlanRepository repository)
    : ICommandHandler<UpdateMaintenancePlanCommand>
{
    public async Task<Result> Handle(UpdateMaintenancePlanCommand command, CancellationToken cancellationToken)
    {
        var plan = await repository.GetByIdAsync(command.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure(MaintenanceErrors.PlanNotFound);
        }

        // Renaming onto another plan's name must be a domain conflict, not a 23505 500 —
        // excluding this very plan keeps the no-rename (or case-tweak) path fine. Stored
        // names are trimmed by the aggregate, so probe with the trimmed name too.
        if (await repository.ExistsByNameAsync(command.Name.Trim(), plan.Id, cancellationToken))
        {
            return Result.Failure(MaintenanceErrors.NameTaken);
        }

        var updateResult = plan.Update(
            command.Name,
            command.VehicleModel,
            command.ServiceClass,
            command.Notes,
            command.Items,
            command.Overhauls);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
