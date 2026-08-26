using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetHistory;

public sealed class GetPmHistoryQueryHandler(IPmReadService readService)
    : IQueryHandler<GetPmHistoryQuery, IReadOnlyList<PmCompletionResponse>>
{
    public async Task<Result<IReadOnlyList<PmCompletionResponse>>> Handle(
        GetPmHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var history = await readService.GetHistoryAsync(query.VehicleId, query.Limit, cancellationToken);
        return history is null
            ? Result.Failure<IReadOnlyList<PmCompletionResponse>>(VehicleErrors.NotFound)
            : Result.Success(history);
    }
}
