using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.SetActive;

public sealed class SetScheduleTemplateActiveCommandHandler(IScheduleTemplateRepository repository)
    : ICommandHandler<SetScheduleTemplateActiveCommand>
{
    public async Task<Result> Handle(SetScheduleTemplateActiveCommand command, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(ScheduleTemplateErrors.NotFound);
        }

        if (command.Active)
        {
            template.Activate();
        }
        else
        {
            template.Deactivate();
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
