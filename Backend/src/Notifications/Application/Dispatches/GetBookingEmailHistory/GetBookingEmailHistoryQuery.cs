using NorthernLink.Shared.Messaging;

namespace NorthernLink.Notifications.Application.Dispatches.GetBookingEmailHistory;

/// <summary>Lists every booking-passes dispatch recorded against a community booking, newest first.</summary>
public sealed record GetBookingEmailHistoryQuery(Guid TenantId, Guid BookingId)
    : IQuery<IReadOnlyList<EmailDispatchResponse>>;
