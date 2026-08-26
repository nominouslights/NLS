using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;

/// <summary>
/// Installs the default preventative-maintenance program ("2016 Ford Transit T-150 /
/// severe", 250 items + 10 overhauls) for the tenant and assigns it to unit NL-01 when
/// that vehicle exists, is not disposed, and follows no plan yet. Idempotent: an existing
/// plan of that name is reused untouched (never recreated or updated), an existing
/// assignment — to any plan — is left alone, and a missing vehicle still seeds the plan.
/// Returns the plan id either way.
/// </summary>
public sealed record SeedDefaultMaintenancePlanCommand(Guid TenantId) : ICommand<Guid>;
