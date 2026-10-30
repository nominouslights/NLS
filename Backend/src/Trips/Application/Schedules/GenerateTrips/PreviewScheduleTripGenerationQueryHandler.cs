using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Schedules.GenerateTrips;

/// <summary>Plan only — <see cref="ScheduleTripMaterializer.ApplyAsync"/> is never called.</summary>
public sealed class PreviewScheduleTripGenerationQueryHandler(ScheduleTripMaterializer materializer, TimeProvider clock)
    : IQueryHandler<PreviewScheduleTripGenerationQuery, ScheduleTripGenerationResult>
{
    public async Task<Result<ScheduleTripGenerationResult>> Handle(
        PreviewScheduleTripGenerationQuery query,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date);

        var plan = await materializer.PlanAsync(query.TemplateId, today, query.Through, cancellationToken);
        return plan.IsSuccess
            ? Result.Success(ScheduleTripMaterializer.Summarize(plan.Value))
            : Result.Failure<ScheduleTripGenerationResult>(plan.Error);
    }
}
