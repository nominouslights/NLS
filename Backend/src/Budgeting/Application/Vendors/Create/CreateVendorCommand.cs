using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.Create;

/// <summary>
/// Adds an (active) vendor to the tenant's register. Returns the new vendor's id.
/// <paramref name="ActorId"/> comes from the signed token's <c>sub</c> claim, never the body.
/// </summary>
public sealed record CreateVendorCommand(Guid TenantId, VendorDetails Details, Guid? ActorId) : ICommand<Guid>;
