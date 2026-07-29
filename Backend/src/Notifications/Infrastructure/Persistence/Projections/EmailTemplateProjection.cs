using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Notifications.Domain.Templates;
using NorthernLink.Notifications.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Notifications.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="EmailTemplate"/> into <c>notifications.rm_email_templates</c>.</summary>
internal sealed class EmailTemplateProjection : NotificationsProjection<EmailTemplate, EmailTemplateReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(EmailTemplate));

    protected override void Map(EmailTemplate source, EmailTemplateReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.ServiceType = source.ServiceType.ToString();
        row.ClientId = source.ClientId;
        row.ClientName = source.ClientName;
        row.Subject = source.Subject;
        row.HtmlBody = source.HtmlBody;
        row.IsActive = source.IsActive;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
