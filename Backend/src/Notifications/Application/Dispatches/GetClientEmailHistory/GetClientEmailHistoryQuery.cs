using NorthernLink.Shared.Messaging;

namespace NorthernLink.Notifications.Application.Dispatches.GetClientEmailHistory;

/// <summary>
/// Lists every email dispatch recorded against a client, newest first — accruals sends and
/// any trip pickup sends that snapshotted this client id.
/// </summary>
public sealed record GetClientEmailHistoryQuery(Guid TenantId, Guid ClientId)
    : IQuery<IReadOnlyList<EmailDispatchResponse>>;
