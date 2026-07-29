using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Templates.Deactivate;

/// <summary>Handles <see cref="DeactivateEmailTemplateCommand"/>.</summary>
public sealed class DeactivateEmailTemplateCommandHandler(IEmailTemplateRepository repository)
    : ICommandHandler<DeactivateEmailTemplateCommand>
{
    public async Task<Result> Handle(DeactivateEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure(EmailTemplateErrors.NotFound);
        }

        var result = template.Deactivate();
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
