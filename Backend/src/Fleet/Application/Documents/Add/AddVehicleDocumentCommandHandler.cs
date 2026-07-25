using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Documents;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Documents.Add;

public sealed class AddVehicleDocumentCommandHandler(IVehicleDocumentRepository repository)
    : ICommandHandler<AddVehicleDocumentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddVehicleDocumentCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.VehicleExistsAsync(command.VehicleId, cancellationToken))
        {
            return Result.Failure<Guid>(VehicleErrors.NotFound);
        }

        var sequence = await repository.NextSequenceAsync(command.TenantId, cancellationToken);
        var number = $"DOC-{sequence}";

        var documentResult = VehicleDocument.Add(
            command.TenantId,
            command.VehicleId,
            number,
            command.Type,
            command.FileName,
            command.FileSizeKb,
            command.UploadedBy,
            command.Expiry,
            command.Note);

        if (documentResult.IsFailure)
        {
            return Result.Failure<Guid>(documentResult.Error);
        }

        repository.Add(documentResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(documentResult.Value.Id);
    }
}
