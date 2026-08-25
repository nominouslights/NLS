using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetHistory;

/// <summary>
/// A vehicle's PM completion history, most recent first (empty when none).
/// <paramref name="Limit"/> caps the page at the newest N entries.
/// </summary>
public sealed record GetPmHistoryQuery(Guid TenantId, Guid VehicleId, int Limit = 200)
    : IQuery<IReadOnlyList<PmCompletionResponse>>;
