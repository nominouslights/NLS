using NorthernLink.Shared.Messaging;

namespace NorthernLink.Notifications.Application.Templates.Deactivate;

/// <summary>Retires a template from the send dialog (templates are never deleted).</summary>
public sealed record DeactivateEmailTemplateCommand(Guid TenantId, Guid TemplateId) : ICommand;
