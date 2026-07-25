using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.Create;

public sealed class CreateScheduleTemplateCommandHandler(
    IScheduleTemplateRepository templateRepository,
    IRouteRepository routeRepository,
    IClientLookupRepository clientLookup)
    : ICommandHandler<CreateScheduleTemplateCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateScheduleTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var route = await routeRepository.GetByIdAsync(command.RouteId, cancellationToken);
        if (route is null)
        {
            return Result.Failure<Guid>(RouteErrors.NotFound);
        }

        var clientName = command.ClientName;
        if (command.ClientId is { } clientId
            && await clientLookup.GetAsync(clientId, cancellationToken) is { } client)
        {
            clientName = client.Name;
        }

        var templateResult = ScheduleTemplate.Create(
            command.TenantId,
            command.Name,
            route.Id,
            command.ServiceType,
            command.ClientId,
            clientName,
            command.DaysOfWeek,
            command.DepartureTime,
            command.ReturnDepartureTime,
            command.SeatsCapacity,
            command.SeatsMinimum,
            command.DefaultVehicleUnit,
            command.DefaultDriverId,
            command.GenerationHorizonDays,
            command.CutoffNote);

        if (templateResult.IsFailure)
        {
            return Result.Failure<Guid>(templateResult.Error);
        }

        templateRepository.Add(templateResult.Value);
        await templateRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(templateResult.Value.Id);
    }
}
