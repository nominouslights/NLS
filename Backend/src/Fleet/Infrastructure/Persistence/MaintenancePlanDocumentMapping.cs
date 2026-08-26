using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// The one place the items/overhauls jsonb mapping lives — the aggregate table
/// (maintenance_plans) and its read-model mirror (rm_maintenance_plans) both call this, so
/// the owned-collection shape, enum-as-string conversions, and decimal precisions can never
/// drift between the write and read side.
/// </summary>
internal static class MaintenancePlanDocumentMapping
{
    public static void MapItemsAndOverhauls<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<MaintenanceItem>?>> items,
        Expression<Func<TEntity, IEnumerable<OverhaulSpec>?>> overhauls)
        where TEntity : class
    {
        builder.OwnsMany(items, item =>
        {
            item.ToJson("items");

            // Explicit, because EF serializes an enum inside owned JSON as an integer by
            // default (same reasoning as TripManifest.Passengers): a jsonb payload gets read
            // by people and hand-written SQL, and an opaque 0/1/2 in there is a trap.
            item.Property(i => i.Tier).HasConversion<string>();
            item.Property(i => i.Task).HasConversion<string>();
        });

        builder.OwnsMany(overhauls, overhaul =>
        {
            overhaul.ToJson("overhauls");
            overhaul.Property(o => o.LabourHours).HasPrecision(8, 2);
            overhaul.Property(o => o.PartsCad).HasPrecision(12, 2);
        });
    }
}
