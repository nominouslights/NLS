using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.AttestPriorRecords;

/// <summary>
/// A compliance officer attests the driver's prior records (s.82(1)(f)) are on file through
/// <paramref name="CoveredThroughDate"/>. The attester recorded is the token's user id; the
/// handler refuses any actor outside <c>Roles.ComplianceAccess</c>.
/// </summary>
public sealed record AttestPriorRecordsCommand(
    Guid TenantId,
    Guid DriverId,
    DateOnly CoveredThroughDate,
    string? Note,
    bool WorksForOtherCarriers) : ICommand<PriorRecordsResponse>;
