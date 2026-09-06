using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.RemoveException;

public sealed class RemoveScheduleExceptionCommandHandler(IScheduleTemplateRepository repository)
    : ICommandHandler<RemoveScheduleExceptionCommand>
{
    public async Task<Result> Handle(RemoveScheduleExceptionCommand command, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(ScheduleTemplateErrors.NotFound);
        }

        var result = template.RemoveException(command.ExceptionId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
