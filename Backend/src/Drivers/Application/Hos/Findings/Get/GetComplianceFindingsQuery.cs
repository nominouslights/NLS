using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Findings.Get;

/// <summary>The driver's findings dated in [<paramref name="From"/>, <paramref name="To"/>] (both optional: open-ended), newest day first.</summary>
public sealed record GetComplianceFindingsQuery(Guid TenantId, Guid DriverId, DateOnly? From, DateOnly? To)
    : IQuery<IReadOnlyList<ComplianceFindingResponse>>;
