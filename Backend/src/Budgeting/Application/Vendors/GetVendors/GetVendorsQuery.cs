using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Vendors.GetVendors;

/// <summary>
/// Lists the tenant's vendor register ordered by name. Retired vendors are left out unless
/// <paramref name="IncludeInactive"/> — the default list is what a picker offers for new work.
/// </summary>
public sealed record GetVendorsQuery(Guid TenantId, bool IncludeInactive) : IQuery<IReadOnlyList<VendorResponse>>;
