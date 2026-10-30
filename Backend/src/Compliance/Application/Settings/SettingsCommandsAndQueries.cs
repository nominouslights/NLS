using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Settings;

public sealed record GetComplianceSettingsQuery(Guid TenantId) : IQuery<ComplianceSettingsResponse>;

/// <summary>Full replace of the tenant's settings row (creates it when absent).</summary>
public sealed record UpdateComplianceSettingsCommand(Guid TenantId, ComplianceSettingsDetails Details, Guid? ActorId) : ICommand;

/// <summary>
/// Seeds everything a tenant needs before the first record: default settings, the CA-MB rule
/// set (published if missing, then applied if the tenant has applied nothing), the twelve
/// document series for the current year, and the regulatory-basis catalogue. Idempotent — a
/// re-run reports zeros and falses.
/// </summary>
public sealed record SeedComplianceDefaultsCommand(Guid TenantId, Guid? ActorId) : ICommand<SeedDefaultsResult>;
