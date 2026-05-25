using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Documents;

internal sealed class DeleteDriverDocumentCommandHandler(
    IDriverRepository driverRepository,
    IFileStorageService fileStorageService)
    : IRequestHandler<DeleteDriverDocumentCommand>
{
    public async Task Handle(DeleteDriverDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await driverRepository.GetDocumentByIdAsync(request.DocumentId, cancellationToken)
            ?? throw new NotFoundException($"Document {request.DocumentId} not found.");

        if (document.DriverId != request.DriverId)
            throw new NotFoundException($"Document {request.DocumentId} does not belong to driver {request.DriverId}.");

        // Delete the stored file bytes first
        await fileStorageService.DeleteAsync(document.StorageKey, cancellationToken);

        // Remove the metadata entity from the aggregate
        var driver = await driverRepository.GetByIdWithDocumentsAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.DriverId} not found.");

        driver.RemoveDocument(request.DocumentId);
        await driverRepository.UpdateAsync(driver, cancellationToken);
    }
}
