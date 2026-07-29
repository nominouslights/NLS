using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of an email dispatch into <c>notifications.rm_email_dispatches</c> —
/// the trip's send-history rows. The recipients jsonb is carried verbatim and re-mapped with
/// the same <c>OwnsMany(...).ToJson(...)</c> shape as the aggregate, so this read model is
/// KEYED (on <see cref="Id"/>) — a keyless entity can't own a jsonb collection.
/// </summary>
public sealed class EmailDispatchReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TripId { get; set; }
    public string TripNumber { get; set; } = null!;
    public Guid? ManifestId { get; set; }
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = null!;
    public string ServiceType { get; set; } = null!;
    public string? ClientName { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset SentAtUtc { get; set; }
    public List<DispatchRecipient> Recipients { get; set; } = [];
    public int Version { get; set; }
}

/// <summary>Maps <see cref="EmailDispatchReadModel"/> to notifications.rm_email_dispatches.</summary>
public sealed class EmailDispatchReadModelConfiguration : IEntityTypeConfiguration<EmailDispatchReadModel>
{
    public void Configure(EntityTypeBuilder<EmailDispatchReadModel> builder)
    {
        builder.HasKey(d => d.Id);
        builder.ToTable("rm_email_dispatches", NotificationsServiceCollectionExtensions.SchemaName);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.TripId).HasColumnName("trip_id");
        builder.Property(d => d.TripNumber).HasColumnName("trip_number");
        builder.Property(d => d.ManifestId).HasColumnName("manifest_id");
        builder.Property(d => d.TemplateId).HasColumnName("template_id");
        builder.Property(d => d.TemplateName).HasColumnName("template_name");
        builder.Property(d => d.ServiceType).HasColumnName("service_type");
        builder.Property(d => d.ClientName).HasColumnName("client_name");
        builder.Property(d => d.Status).HasColumnName("status");
        builder.Property(d => d.SentAtUtc).HasColumnName("sent_at_utc");
        builder.Property(d => d.Version).HasColumnName("version");

        builder.OwnsMany(d => d.Recipients, recipient =>
        {
            recipient.ToJson("recipients");
            recipient.Property(r => r.Status).HasConversion<string>();
        });

        builder.HasIndex(d => new { d.TenantId, d.TripId });
    }
}
