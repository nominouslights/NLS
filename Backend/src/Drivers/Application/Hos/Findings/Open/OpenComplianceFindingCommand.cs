using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Findings.Open;

/// <summary>A compliance officer opens a finding against <paramref name="LocalDate"/>. <paramref name="Rule"/> is an <c>HosRule</c> name or a record-keeping offence.</summary>
public sealed record OpenComplianceFindingCommand(
    Guid TenantId,
    Guid DriverId,
    DateOnly LocalDate,
    string? Rule,
    FindingSeverity Severity,
    string? Description,
    FindingSource Source,
    string? FollowUpNote) : ICommand<ComplianceFindingResponse>;
