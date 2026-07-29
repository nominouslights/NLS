using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Notifications.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="EmailDispatch"/> into <c>notifications.rm_email_dispatches</c>. The
/// recipient collection is copied into a fresh list (it persists as owned jsonb on both
/// sides), so the read row never aliases the aggregate's state.
/// </summary>
internal sealed class EmailDispatchProjection : NotificationsProjection<EmailDispatch, EmailDispatchReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(EmailDispatch));

    protected override void Map(EmailDispatch source, EmailDispatchReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.TripId = source.TripId;
        row.TripNumber = source.TripNumber;
        row.ManifestId = source.ManifestId;
        row.TemplateId = source.TemplateId;
        row.TemplateName = source.TemplateName;
        row.ServiceType = source.ServiceType.ToString();
        row.ClientName = source.ClientName;
        row.Status = source.Status.ToString();
        row.SentAtUtc = source.SentAtUtc;
        row.Recipients = [.. source.Recipients];
        row.Version = source.Version;
    }
}
