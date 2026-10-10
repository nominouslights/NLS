using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Budgeting.Domain.Vendors;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="Vendor"/> into <c>budgeting.rm_vendors</c>.</summary>
internal sealed class VendorProjection : BudgetingProjection<Vendor, VendorReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(Vendor));

    protected override void Map(Vendor source, VendorReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.NormalizedName = source.NormalizedName;
        row.ContactName = source.ContactName;
        row.Email = source.Email;
        row.Phone = source.Phone;
        row.Address = source.Address;
        row.Notes = source.Notes;
        row.GstRegistrationNumber = source.GstRegistrationNumber;
        row.QboDisplayName = source.QboDisplayName;
        row.DefaultBudgetCode = source.DefaultBudgetCode;
        row.IsActive = source.IsActive;
        row.CreatedBy = source.CreatedBy;
        row.ModifiedBy = source.ModifiedBy;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
