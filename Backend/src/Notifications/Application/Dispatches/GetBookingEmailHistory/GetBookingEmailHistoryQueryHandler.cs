using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;

namespace NorthernLink.Notifications.Application.Dispatches.GetBookingEmailHistory;

/// <summary>Handles <see cref="GetBookingEmailHistoryQuery"/>.</summary>
public sealed class GetBookingEmailHistoryQueryHandler(IEmailDispatchReadService readService)
    : IQueryHandler<GetBookingEmailHistoryQuery, IReadOnlyList<EmailDispatchResponse>>
{
    public async Task<Result<IReadOnlyList<EmailDispatchResponse>>> Handle(
        GetBookingEmailHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var dispatches = await readService.GetForBookingAsync(query.BookingId, cancellationToken);
        return Result.Success(dispatches);
    }
}
