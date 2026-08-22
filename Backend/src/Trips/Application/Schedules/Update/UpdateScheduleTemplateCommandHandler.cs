using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.Update;

public sealed class UpdateScheduleTemplateCommandHandler(
    IScheduleTemplateRepository templateRepository,
    IRouteRepository routeRepository,
    IClientLookupRepository clientLookup)
    : ICommandHandler<UpdateScheduleTemplateCommand>
{
    public async Task<Result> Handle(UpdateScheduleTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(ScheduleTemplateErrors.NotFound);
        }

        var route = await routeRepository.GetByIdAsync(command.RouteId, cancellationToken);
        if (route is null)
        {
            return Result.Failure(RouteErrors.NotFound);
        }

        var clientName = command.ClientName;
        if (command.ClientId is { } clientId
            && await clientLookup.GetAsync(clientId, cancellationToken) is { } client)
        {
            clientName = client.Name;
        }

        var result = template.Update(
            command.Name,
            route.Id,
            command.ServiceType,
            command.ClientId,
            clientName,
            command.RecurrenceKind,
            command.DaysOfWeek,
            command.IntervalDays,
            command.AnchorDate,
            command.DaysOfMonth,
            command.DepartureTime,
            command.ReturnDepartureTime,
            command.SeatsCapacity,
            command.SeatsMinimum,
            command.DefaultVehicleUnit,
            command.DefaultDriverId,
            command.GenerationHorizonDays,
            command.CutoffNote);

        if (result.IsFailure)
        {
            return result;
        }

        await templateRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
