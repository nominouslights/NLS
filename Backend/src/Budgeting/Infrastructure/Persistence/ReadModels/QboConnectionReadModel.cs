using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a QuickBooks connection into <c>budgeting.rm_qbo_connections</c>.
/// Enum values are projected as their PascalCase names. Like the aggregate, it carries no token.
/// <para>
/// The connection screen itself reads the write table (see <c>GetQboConnectionQueryHandler</c>
/// for why); this row exists for the module's read side generally — reports and the import's
/// tenant enumeration — under the same convention as every other aggregate.
/// </para>
/// </summary>
public sealed class QboConnectionReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RealmId { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string Environment { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid ConnectedBy { get; set; }
    public DateTimeOffset ConnectedAtUtc { get; set; }
    public DateTimeOffset RefreshTokenExpiresAtUtc { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAtUtc { get; set; }
    public DateTimeOffset? LastSyncCursorUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="QboConnectionReadModel"/> to budgeting.rm_qbo_connections.</summary>
public sealed class QboConnectionReadModelConfiguration : IEntityTypeConfiguration<QboConnectionReadModel>
{
    public void Configure(EntityTypeBuilder<QboConnectionReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_qbo_connections", BudgetingServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.RealmId).HasColumnName("realm_id");
        builder.Property(c => c.CompanyName).HasColumnName("company_name");
        builder.Property(c => c.Environment).HasColumnName("environment");
        builder.Property(c => c.Status).HasColumnName("status");
        builder.Property(c => c.ConnectedBy).HasColumnName("connected_by");
        builder.Property(c => c.ConnectedAtUtc).HasColumnName("connected_at_utc");
        builder.Property(c => c.RefreshTokenExpiresAtUtc).HasColumnName("refresh_token_expires_at_utc");
        builder.Property(c => c.LastSuccessfulSyncAtUtc).HasColumnName("last_successful_sync_at_utc");
        builder.Property(c => c.LastSyncCursorUtc).HasColumnName("last_sync_cursor_utc");
        builder.Property(c => c.LastErrorCode).HasColumnName("last_error_code");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(c => c.Version).HasColumnName("version");

        builder.HasIndex(c => c.TenantId);
    }
}
