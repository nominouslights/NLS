using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>
/// Maps the EmailDispatch aggregate to notifications.email_dispatches (snake_case columns).
/// Per-recipient outcomes are an owned collection mapped to jsonb — one row per dispatch, no
/// child table (outcomes are written once when the dispatch is recorded, never addressed
/// individually by SQL).
/// </summary>
public sealed class EmailDispatchConfiguration : IEntityTypeConfiguration<EmailDispatch>
{
    public void Configure(EntityTypeBuilder<EmailDispatch> builder)
    {
        builder.ToTable("email_dispatches");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.TripId).HasColumnName("trip_id");
        builder.Property(d => d.TripNumber).HasColumnName("trip_number").HasMaxLength(32);
        builder.Property(d => d.ManifestId).HasColumnName("manifest_id");
        builder.Property(d => d.TemplateId).HasColumnName("template_id");
        builder.Property(d => d.TemplateName).HasColumnName("template_name").HasMaxLength(200);

        builder.Property(d => d.ServiceType)
            .HasColumnName("service_type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(d => d.ClientId).HasColumnName("client_id");
        builder.Property(d => d.ClientName).HasColumnName("client_name").HasMaxLength(200);

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(d => d.SentAtUtc).HasColumnName("sent_at_utc");

        builder.OwnsMany(d => d.Recipients, recipient =>
        {
            recipient.ToJson("recipients");
            recipient.Property(r => r.Status).HasConversion<string>();
        });

        builder.HasIndex(d => new { d.TenantId, d.TripId });

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
