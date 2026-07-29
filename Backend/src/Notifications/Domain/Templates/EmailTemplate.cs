using NorthernLink.Shared.Kernel;
using NorthernLink.Notifications.Domain.Templates.Events;

namespace NorthernLink.Notifications.Domain.Templates;

/// <summary>
/// A reusable pickup-email template: simple HTML plus <see cref="MergeFields"/> tokens,
/// tagged with the <see cref="ServiceType"/> it applies to and optionally pinned to one
/// specific client (<see cref="ClientId"/> with a <see cref="ClientName"/> snapshot — the
/// Notifications module never references the Clients module, so the name travels with the
/// template). Merge tokens are validated against the canonical set on every create/update,
/// so a typo fails at save time. Templates are deactivated, never deleted — dispatch history
/// snapshots the template id and name, so history survives regardless.
/// </summary>
public sealed class EmailTemplate : AggregateRoot, ITenantScoped
{
    /// <summary>Maximum length of <see cref="Name"/>.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Maximum length of <see cref="Subject"/>.</summary>
    public const int SubjectMaxLength = 300;

    /// <summary>Maximum length of <see cref="HtmlBody"/>.</summary>
    public const int HtmlBodyMaxLength = 20_000;

    private EmailTemplate()
    {
        // EF Core materialization only.
        Name = null!;
        Subject = null!;
        HtmlBody = null!;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public NotificationServiceType ServiceType { get; private set; }
    public Guid? ClientId { get; private set; }
    public string? ClientName { get; private set; }
    public string Subject { get; private set; }
    public string HtmlBody { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Creates an active template after validating fields and merge tokens.</summary>
    public static Result<EmailTemplate> Create(
        Guid tenantId,
        string name,
        NotificationServiceType serviceType,
        Guid? clientId,
        string? clientName,
        string subject,
        string htmlBody)
    {
        if (Validate(name, clientId, clientName, subject, htmlBody) is { } error)
        {
            return Result.Failure<EmailTemplate>(error);
        }

        var now = DateTimeOffset.UtcNow;
        var template = new EmailTemplate
        {
            TenantId = tenantId,
            Name = name.Trim(),
            ServiceType = serviceType,
            ClientId = clientId,
            ClientName = clientId is null ? null : clientName!.Trim(),
            Subject = subject.Trim(),
            HtmlBody = htmlBody,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        template.Raise(new EmailTemplateCreatedDomainEvent(template.Id, tenantId));
        return Result.Success(template);
    }

    /// <summary>Replaces the template's content after the same validation as create.</summary>
    public Result Update(
        string name,
        NotificationServiceType serviceType,
        Guid? clientId,
        string? clientName,
        string subject,
        string htmlBody)
    {
        if (Validate(name, clientId, clientName, subject, htmlBody) is { } error)
        {
            return Result.Failure(error);
        }

        Name = name.Trim();
        ServiceType = serviceType;
        ClientId = clientId;
        ClientName = clientId is null ? null : clientName!.Trim();
        Subject = subject.Trim();
        HtmlBody = htmlBody;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new EmailTemplateUpdatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>Makes the template selectable for sending again.</summary>
    public Result Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new EmailTemplateActivatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>
    /// Retires the template from the send dialog. Deactivation is the platform's delete —
    /// dispatch history references templates by id, so rows are never removed.
    /// </summary>
    public Result Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new EmailTemplateDeactivatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    private static Error? Validate(string name, Guid? clientId, string? clientName, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return EmailTemplateErrors.NameRequired;
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return EmailTemplateErrors.NameTooLong;
        }

        if (clientId is not null && string.IsNullOrWhiteSpace(clientName))
        {
            return EmailTemplateErrors.ClientNameRequired;
        }

        if (clientId is null && !string.IsNullOrWhiteSpace(clientName))
        {
            return EmailTemplateErrors.ClientNameWithoutClientId;
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return EmailTemplateErrors.SubjectRequired;
        }

        if (subject.Trim().Length > SubjectMaxLength)
        {
            return EmailTemplateErrors.SubjectTooLong;
        }

        if (string.IsNullOrWhiteSpace(htmlBody))
        {
            return EmailTemplateErrors.HtmlBodyRequired;
        }

        if (htmlBody.Length > HtmlBodyMaxLength)
        {
            return EmailTemplateErrors.HtmlBodyTooLong;
        }

        var unknown = MergeFields.UnknownTokensIn(subject).Concat(MergeFields.UnknownTokensIn(htmlBody)).ToList();
        if (unknown.Count > 0)
        {
            return EmailTemplateErrors.UnknownMergeField(unknown[0]);
        }

        return null;
    }
}
