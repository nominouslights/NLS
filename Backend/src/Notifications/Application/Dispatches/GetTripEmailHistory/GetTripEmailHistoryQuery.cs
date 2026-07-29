using NorthernLink.Shared.Messaging;

namespace NorthernLink.Notifications.Application.Dispatches.GetTripEmailHistory;

/// <summary>Lists every email dispatch recorded against a trip, newest first.</summary>
public sealed record GetTripEmailHistoryQuery(Guid TenantId, Guid TripId)
    : IQuery<IReadOnlyList<EmailDispatchResponse>>;
