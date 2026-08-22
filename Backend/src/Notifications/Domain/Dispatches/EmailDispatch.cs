using NorthernLink.Shared.Kernel;
using NorthernLink.Notifications.Domain.Dispatches.Events;

namespace NorthernLink.Notifications.Domain.Dispatches;

/// <summary>
/// One send action against a trip's manifest — the immutable history record dispatchers see
/// when they reopen the send dialog. The aggregate id IS the client-generated dispatch id,
/// which is what makes the send endpoint idempotent: a replayed POST finds this row and
/// returns it instead of emailing everyone twice. Trip context (trip id/number, manifest id,
/// client id + name) and the template (id + name) are opaque snapshots from the caller —
/// Notifications never queries Trips or Clients. Per-recipient outcomes are embedded as jsonb via
/// <see cref="DispatchRecipient"/>; <see cref="Status"/> is derived from them, never stored
/// independently.
/// </summary>
public sealed class EmailDispatch : AggregateRoot, ITenantScoped
{
    /// <summary>Server-side recipient cap (manifests max out at 8 passengers; 16 leaves slack).</summary>
    public const int MaxRecipients = 16;

    private readonly List<DispatchRecipient> _recipients = [];

    private EmailDispatch()
    {
        // EF Core materialization only.
        TripNumber = null!;
        TemplateName = null!;
    }

    private EmailDispatch(Guid dispatchId)
        : this()
    {
        Id = dispatchId;
    }

    public Guid TenantId { get; private set; }
    public Guid TripId { get; private set; }
    public string TripNumber { get; private set; }
    public Guid? ManifestId { get; private set; }
    public Guid TemplateId { get; private set; }
    public string TemplateName { get; private set; }
    public NotificationServiceType ServiceType { get; private set; }
    public Guid? ClientId { get; private set; }
    public string? ClientName { get; private set; }
    public EmailDispatchStatus Status { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }
    public IReadOnlyList<DispatchRecipient> Recipients => _recipients;

    /// <summary>
    /// Records a completed send action (whatever its outcomes — total failure is history
    /// the dispatcher must see too). <paramref name="dispatchId"/> is the client-generated
    /// idempotency key and becomes the aggregate id.
    /// </summary>
    public static Result<EmailDispatch> Record(
        Guid dispatchId,
        Guid tenantId,
        Guid tripId,
        string tripNumber,
        Guid? manifestId,
        Guid templateId,
        string templateName,
        NotificationServiceType serviceType,
        Guid? clientId,
        string? clientName,
        IReadOnlyList<DispatchRecipient> recipients)
    {
        if (recipients.Count == 0)
        {
            return Result.Failure<EmailDispatch>(EmailDispatchErrors.NoRecipients);
        }

        if (recipients.Count > MaxRecipients)
        {
            return Result.Failure<EmailDispatch>(EmailDispatchErrors.TooManyRecipients);
        }

        var dispatch = new EmailDispatch(dispatchId)
        {
            TenantId = tenantId,
            TripId = tripId,
            TripNumber = tripNumber.Trim(),
            ManifestId = manifestId,
            TemplateId = templateId,
            TemplateName = templateName,
            ServiceType = serviceType,
            ClientId = clientId,
            ClientName = string.IsNullOrWhiteSpace(clientName) ? null : clientName.Trim(),
            Status = DeriveStatus(recipients),
            SentAtUtc = DateTimeOffset.UtcNow,
        };
        dispatch._recipients.AddRange(recipients);

        dispatch.Raise(new EmailDispatchRecordedDomainEvent(dispatchId, tenantId));
        return Result.Success(dispatch);
    }

    private static EmailDispatchStatus DeriveStatus(IReadOnlyList<DispatchRecipient> recipients)
    {
        var sent = recipients.Count(r => r.Status == DispatchRecipientStatus.Sent);
        return sent == recipients.Count ? EmailDispatchStatus.Sent
            : sent == 0 ? EmailDispatchStatus.Failed
            : EmailDispatchStatus.PartiallyFailed;
    }
}
