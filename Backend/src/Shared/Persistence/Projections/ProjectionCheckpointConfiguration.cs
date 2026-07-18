using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>
/// Maps <see cref="ProjectionCheckpoint"/> to <c>&lt;module-schema&gt;.projection_checkpoints</c>
/// (snake_case columns). Applied centrally from <c>ModuleDbContext</c> so every module schema
/// gets the identical shape, exactly like the three audit-table configs.
/// </summary>
public sealed class ProjectionCheckpointConfiguration : IEntityTypeConfiguration<ProjectionCheckpoint>
{
    public void Configure(EntityTypeBuilder<ProjectionCheckpoint> builder)
    {
        builder.ToTable("projection_checkpoints");

        builder.HasKey(c => c.ProjectionName);
        builder.Property(c => c.ProjectionName).HasColumnName("projection_name").HasMaxLength(64);
        builder.Property(c => c.LastPosition).HasColumnName("last_position");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
