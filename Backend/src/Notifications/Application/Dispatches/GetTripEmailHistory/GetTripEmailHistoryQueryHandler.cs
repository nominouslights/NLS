using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;

namespace NorthernLink.Notifications.Application.Dispatches.GetTripEmailHistory;

/// <summary>Handles <see cref="GetTripEmailHistoryQuery"/>.</summary>
public sealed class GetTripEmailHistoryQueryHandler(IEmailDispatchReadService readService)
    : IQueryHandler<GetTripEmailHistoryQuery, IReadOnlyList<EmailDispatchResponse>>
{
    public async Task<Result<IReadOnlyList<EmailDispatchResponse>>> Handle(
        GetTripEmailHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var dispatches = await readService.GetForTripAsync(query.TripId, cancellationToken);
        return Result.Success(dispatches);
    }
}
