using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Templates.Activate;

/// <summary>Handles <see cref="ActivateEmailTemplateCommand"/>.</summary>
public sealed class ActivateEmailTemplateCommandHandler(IEmailTemplateRepository repository)
    : ICommandHandler<ActivateEmailTemplateCommand>
{
    public async Task<Result> Handle(ActivateEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(EmailTemplateErrors.NotFound);
        }

        var result = template.Activate();
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
