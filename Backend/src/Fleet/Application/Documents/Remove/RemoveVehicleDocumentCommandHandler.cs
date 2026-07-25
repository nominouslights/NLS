using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Documents;

namespace NorthernLink.Fleet.Application.Documents.Remove;

public sealed class RemoveVehicleDocumentCommandHandler(IVehicleDocumentRepository repository)
    : ICommandHandler<RemoveVehicleDocumentCommand>
{
    public async Task<Result> Handle(RemoveVehicleDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(command.DocumentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure(DocumentErrors.NotFound);
        }

        repository.Remove(document);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
