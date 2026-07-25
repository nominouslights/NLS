using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a work order, via <c>fleet.mv_work_orders</c> / <c>fleet.v_work_orders</c>.
/// <see cref="LineItems"/> is carried verbatim as the base table's text[] column.
/// </summary>
public sealed class WorkOrderReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public string Number { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Priority { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string? SourceRef { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public string? AssignedTo { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public List<string> LineItems { get; set; } = [];
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? ResolvingServiceId { get; set; }
    public Guid? ShopId { get; set; }
    public decimal? AuthorizedLimitCad { get; set; }
    public string? BudgetCode { get; set; }
    public DateTimeOffset? DateRequiredOrOos { get; set; }
    public int Version { get; set; }
}

public sealed class WorkOrderReadModelConfiguration : IEntityTypeConfiguration<WorkOrderReadModel>
{
    public void Configure(EntityTypeBuilder<WorkOrderReadModel> builder)
    {
        builder.HasKey(w => w.Id);
        builder.ToTable("rm_work_orders", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.TenantId).HasColumnName("tenant_id");
        builder.Property(w => w.VehicleId).HasColumnName("vehicle_id");
        builder.Property(w => w.Number).HasColumnName("number");
        builder.Property(w => w.Title).HasColumnName("title");
        builder.Property(w => w.Description).HasColumnName("description");
        builder.Property(w => w.Status).HasColumnName("status");
        builder.Property(w => w.Priority).HasColumnName("priority");
        builder.Property(w => w.Source).HasColumnName("source");
        builder.Property(w => w.SourceRef).HasColumnName("source_ref");
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.AssignedTo).HasColumnName("assigned_to");
        builder.Property(w => w.DueDate).HasColumnName("due_date");
        builder.Property(w => w.LineItems).HasColumnName("line_items").HasColumnType("text[]");
        builder.Property(w => w.CompletedAt).HasColumnName("completed_at");
        builder.Property(w => w.ResolvingServiceId).HasColumnName("resolving_service_id");
        builder.Property(w => w.ShopId).HasColumnName("shop_id");
        builder.Property(w => w.AuthorizedLimitCad).HasColumnName("authorized_limit_cad");
        builder.Property(w => w.BudgetCode).HasColumnName("budget_code");
        builder.Property(w => w.DateRequiredOrOos).HasColumnName("date_required_or_oos");
        builder.Property(w => w.Version).HasColumnName("version");
    }
}
