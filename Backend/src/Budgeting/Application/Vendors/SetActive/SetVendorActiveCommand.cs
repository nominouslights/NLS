using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Vendors.SetActive;

/// <summary>
/// Retires (<c>IsActive = false</c>) or restores a vendor — the normal end of a vendor's life,
/// not a delete. <paramref name="ActorId"/> stamps <c>modified_by</c>.
/// </summary>
public sealed record SetVendorActiveCommand(
    Guid TenantId,
    Guid VendorId,
    bool IsActive,
    Guid? ActorId) : ICommand;
