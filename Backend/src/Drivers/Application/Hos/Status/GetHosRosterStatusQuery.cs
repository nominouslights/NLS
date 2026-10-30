using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Status;

/// <summary>
/// Every roster driver's band and tightest remaining figure in one call — the Dispatch
/// Console's roster "HOS left" column. Drivers with no ledger are included (band Off, nothing
/// remaining, <c>NoLedger</c>), never omitted: a missing row would read as "no problem".
/// </summary>
public sealed record GetHosRosterStatusQuery(Guid TenantId) : IQuery<IReadOnlyList<HosRosterStatusResponse>>;
