using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;

namespace NorthernLink.Notifications.Application.Dispatches.GetClientEmailHistory;

/// <summary>Handles <see cref="GetClientEmailHistoryQuery"/>.</summary>
public sealed class GetClientEmailHistoryQueryHandler(IEmailDispatchReadService readService)
    : IQueryHandler<GetClientEmailHistoryQuery, IReadOnlyList<EmailDispatchResponse>>
{
    public async Task<Result<IReadOnlyList<EmailDispatchResponse>>> Handle(
        GetClientEmailHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var dispatches = await readService.GetForClientAsync(query.ClientId, cancellationToken);
        return Result.Success(dispatches);
    }
}
