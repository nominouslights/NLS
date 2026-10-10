using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Vendors.Delete;

/// <summary>
/// Permanently removes a vendor created in error that nothing references. Anything referenced is
/// retired instead. No ActorId, the <c>DeleteBudgetCodeCommand</c> reasoning: a deleted row has
/// nowhere to record who deleted it, though the audit pipeline still journals the final state.
/// </summary>
public sealed record DeleteVendorCommand(Guid TenantId, Guid VendorId) : ICommand;
