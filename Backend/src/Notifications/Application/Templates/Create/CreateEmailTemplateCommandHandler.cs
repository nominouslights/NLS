using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Templates.Create;

/// <summary>Handles <see cref="CreateEmailTemplateCommand"/>.</summary>
public sealed class CreateEmailTemplateCommandHandler(IEmailTemplateRepository repository)
    : ICommandHandler<CreateEmailTemplateCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        var templateResult = EmailTemplate.Create(
            command.TenantId,
            command.Name,
            command.ServiceType,
            command.ClientId,
            command.ClientName,
            command.Subject,
            command.HtmlBody);

        if (templateResult.IsFailure)
        {
            return Result.Failure<Guid>(templateResult.Error);
        }

        repository.Add(templateResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(templateResult.Value.Id);
    }
}
