using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Vendors.GetVendorById;

/// <summary>One vendor of the tenant's register, active or retired.</summary>
public sealed record GetVendorByIdQuery(Guid TenantId, Guid VendorId) : IQuery<VendorResponse>;
