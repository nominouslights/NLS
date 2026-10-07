using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// The one place the work order <c>defects</c> jsonb mapping lives — the aggregate table
/// (work_orders) and its read-model mirror (rm_work_orders) both call this, so the enum-as-name
/// conversions can never drift between writer and reader (a conversion on one side only has the
/// projector write "OutOfService" while the reader expects 2 — a runtime-only failure).
///
/// The column is <c>jsonb NOT NULL DEFAULT '[]'</c> (migration AddWorkOrderDefectLines). EF
/// cannot mark an owned COLLECTION required, so the model still says nullable; the NOT NULL and
/// the default are set in the migration by hand. That is safe because both entities initialise
/// the list to <c>[]</c> and EF writes an empty collection as <c>[]</c>, never NULL — and the
/// default backfills every existing work order as "no lines" (the legacy path) with no data
/// migration.
/// </summary>
internal static class WorkOrderDefectLineMapping
{
    public static void MapDefects<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<WorkOrderDefectLine>?>> defects)
        where TEntity : class
    {
        builder.OwnsMany(defects, line =>
        {
            line.ToJson("defects");
            line.Property(l => l.Severity).HasConversion<string>();
            line.Property(l => l.Outcome).HasConversion<string>();
        });
    }
}
