using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Audit;

/// <summary>
/// The compliance audit: one row per America/Winnipeg day in [<paramref name="From"/>,
/// <paramref name="To"/>] (capped at today, at most 92 days) with everything that bears on it.
/// ComplianceAccess only — the endpoint's group carries the policy.
/// </summary>
public sealed record GetHosAuditQuery(Guid TenantId, Guid DriverId, DateOnly From, DateOnly To)
    : IQuery<IReadOnlyList<HosAuditDayResponse>>;
