using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.UpdateException;

public sealed class UpdateScheduleExceptionCommandHandler(IScheduleTemplateRepository repository)
    : ICommandHandler<UpdateScheduleExceptionCommand>
{
    public async Task<Result> Handle(UpdateScheduleExceptionCommand command, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(ScheduleTemplateErrors.NotFound);
        }

        var result = template.UpdateException(
            command.ExceptionId,
            command.Date,
            command.Kind,
            command.DepartureTime,
            command.ReturnDepartureTime,
            command.Note);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
