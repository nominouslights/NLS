using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Vendors.GetPlannedRollup;

/// <summary>
/// One period's planned expense per vendor, plus the "No vendor" bucket
/// (<see cref="VendorPlannedRollup"/>). Planned only — actuals are a later slice.
/// </summary>
public sealed record GetVendorRollupQuery(Guid TenantId, Guid PeriodId) : IQuery<VendorRollupResponse>;
