using NorthernLink.Shared.Messaging;

namespace NorthernLink.Notifications.Application.Templates.Activate;

/// <summary>Makes a template selectable for sending again.</summary>
public sealed record ActivateEmailTemplateCommand(Guid TenantId, Guid TemplateId) : ICommand;
