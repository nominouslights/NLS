using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Templates.Create;

/// <summary>Handles <see cref="CreateEmailTemplateCommand"/>.</summary>
public sealed class CreateEmailTemplateCommandHandler(
    IEmailTemplateRepository repository,
    IEmailHtmlSanitizer htmlSanitizer)
    : ICommandHandler<CreateEmailTemplateCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        // Allowlist-sanitize the raw template body before it is stored/sent (defense-in-depth;
        // merge tokens survive as text nodes). Subject stays untouched — plain text, not an HTML sink.
        var templateResult = EmailTemplate.Create(
            command.TenantId,
            command.Name,
            command.ServiceType,
            command.ClientId,
            command.ClientName,
            command.Subject,
            htmlSanitizer.Sanitize(command.HtmlBody));

        if (templateResult.IsFailure)
        {
            return Result.Failure<Guid>(templateResult.Error);
        }

        repository.Add(templateResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(templateResult.Value.Id);
    }
}
