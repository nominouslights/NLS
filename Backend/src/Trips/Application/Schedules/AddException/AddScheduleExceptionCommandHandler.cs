using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.AddException;

public sealed class AddScheduleExceptionCommandHandler(IScheduleTemplateRepository repository)
    : ICommandHandler<AddScheduleExceptionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddScheduleExceptionCommand command, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure<Guid>(ScheduleTemplateErrors.NotFound);
        }

        var result = template.AddException(
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
        return result;
    }
}
