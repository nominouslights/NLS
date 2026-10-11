using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Findings.Update;

/// <summary>A compliance officer moves a finding along Open → Corrected → Closed and/or updates its follow-up note.</summary>
public sealed record UpdateComplianceFindingCommand(
    Guid TenantId,
    Guid DriverId,
    Guid FindingId,
    FindingStatus Status,
    string? FollowUpNote) : ICommand<ComplianceFindingResponse>;
