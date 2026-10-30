using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// The one place the budget item <c>vendor_shares</c> jsonb mapping lives — the aggregate table
/// (budget_allocations) and its read-model mirror (rm_budget_allocations) both call this, so the
/// two can never drift (the Fleet <c>WorkOrderDefectLineMapping</c> precedent). The JSON keys are
/// the CLR names, <c>VendorId</c> and <c>AmountCad</c> — the AddBudgetItemVendorShares backfill
/// writes exactly those keys, and the vendor usage probe queries <c>VendorId</c>.
/// <para>
/// The column is <c>jsonb NOT NULL DEFAULT '[]'</c> (migration AddBudgetItemVendorShares). EF
/// cannot mark an owned COLLECTION required, so the model still says nullable; the NOT NULL and the
/// default are set in the migration by hand. That is safe because both entities initialise the
/// list to <c>[]</c> and EF writes an empty collection as <c>[]</c>, never NULL.
/// </para>
/// </summary>
internal static class BudgetItemVendorShareMapping
{
    public const string ColumnName = "vendor_shares";

    public static void MapVendorShares<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<BudgetItemVendorShare>?>> shares)
        where TEntity : class
    {
        builder.OwnsMany(shares, share =>
        {
            share.ToJson(ColumnName);
            share.Property(s => s.VendorId);
            share.Property(s => s.AmountCad).HasPrecision(12, 2);
        });
    }
}
