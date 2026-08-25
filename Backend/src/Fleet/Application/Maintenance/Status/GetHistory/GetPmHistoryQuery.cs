using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetHistory;

/// <summary>
/// A vehicle's PM completion history, most recent first (empty when none).
/// <paramref name="Limit"/> caps the page at the newest N entries (default and ceiling from
/// <see cref="IPmReadService"/> — the single home of the history limits).
/// </summary>
public sealed record GetPmHistoryQuery(
    Guid TenantId, Guid VehicleId, int Limit = IPmReadService.DefaultHistoryLimit)
    : IQuery<IReadOnlyList<PmCompletionResponse>>;
