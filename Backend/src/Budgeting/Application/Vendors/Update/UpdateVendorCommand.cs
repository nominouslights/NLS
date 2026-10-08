using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.Update;

/// <summary>
/// Rewrites every editable field of a vendor (a PUT — omitted optional fields are cleared). The
/// name may change; it must stay unique ignoring case. <paramref name="ActorId"/> stamps
/// <c>modified_by</c>.
/// </summary>
public sealed record UpdateVendorCommand(
    Guid TenantId,
    Guid VendorId,
    VendorDetails Details,
    Guid? ActorId) : ICommand;
