using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Schedules.GenerateTrips;

/// <summary>
/// Plan then apply through <see cref="ScheduleTripMaterializer"/>. "Today" is the UTC date —
/// the worker's convention, so the two paths agree on which occurrences are in the window.
/// </summary>
public sealed class GenerateScheduleTripsCommandHandler(ScheduleTripMaterializer materializer, TimeProvider clock)
    : ICommandHandler<GenerateScheduleTripsCommand, ScheduleTripGenerationResult>
{
    public async Task<Result<ScheduleTripGenerationResult>> Handle(
        GenerateScheduleTripsCommand command,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date);

        var plan = await materializer.PlanAsync(command.TemplateId, today, command.Through, cancellationToken);
        if (plan.IsFailure)
        {
            return Result.Failure<ScheduleTripGenerationResult>(plan.Error);
        }

        return await materializer.ApplyAsync(command.TenantId, plan.Value, cancellationToken);
    }
}
