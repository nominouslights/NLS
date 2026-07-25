using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Maps the WorkOrder aggregate to fleet.work_orders. LineItems is a Postgres text[] column.</summary>
public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_orders");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(w => w.TenantId).HasColumnName("tenant_id");
        builder.Property(w => w.VehicleId).HasColumnName("vehicle_id");
        builder.Property(w => w.Number).HasColumnName("number").HasMaxLength(16);
        builder.Property(w => w.Title).HasColumnName("title").HasMaxLength(300);
        builder.Property(w => w.Description).HasColumnName("description").HasMaxLength(2000);

        builder.Property(w => w.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(w => w.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(w => w.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(w => w.SourceRef).HasColumnName("source_ref").HasMaxLength(64);
        builder.Property(w => w.CreatedBy).HasColumnName("created_by").HasMaxLength(128);
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.AssignedTo).HasColumnName("assigned_to").HasMaxLength(200);
        builder.Property(w => w.DueDate).HasColumnName("due_date");
        builder.Property(w => w.LineItems).HasColumnName("line_items").HasColumnType("text[]");
        builder.Property(w => w.CompletedAt).HasColumnName("completed_at");
        builder.Property(w => w.ResolvingServiceId).HasColumnName("resolving_service_id");
        builder.Property(w => w.ShopId).HasColumnName("shop_id");
        builder.Property(w => w.AuthorizedLimitCad).HasColumnName("authorized_limit_cad").HasColumnType("numeric(12,2)");
        builder.Property(w => w.BudgetCode).HasColumnName("budget_code").HasMaxLength(64);
        builder.Property(w => w.DateRequiredOrOos).HasColumnName("date_required_or_oos");

        builder.HasIndex(w => new { w.TenantId, w.Number }).IsUnique();
        builder.HasIndex(w => new { w.TenantId, w.VehicleId });
    }
}
