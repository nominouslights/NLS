using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Templates.Update;

/// <summary>Handles <see cref="UpdateEmailTemplateCommand"/>.</summary>
public sealed class UpdateEmailTemplateCommandHandler(IEmailTemplateRepository repository)
    : ICommandHandler<UpdateEmailTemplateCommand>
{
    public async Task<Result> Handle(UpdateEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(EmailTemplateErrors.NotFound);
        }

        var result = template.Update(
            command.Name,
            command.ServiceType,
            command.ClientId,
            command.ClientName,
            command.Subject,
            command.HtmlBody);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
