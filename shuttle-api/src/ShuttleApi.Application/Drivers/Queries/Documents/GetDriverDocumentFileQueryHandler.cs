using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Queries.Documents;

internal sealed class GetDriverDocumentFileQueryHandler(
    IDriverRepository driverRepository,
    IFileStorageService fileStorageService)
    : IRequestHandler<GetDriverDocumentFileQuery, DriverDocumentFileResult>
{
    public async Task<DriverDocumentFileResult> Handle(
        GetDriverDocumentFileQuery request,
        CancellationToken cancellationToken)
    {
        var document = await driverRepository.GetDocumentByIdAsync(request.DocumentId, cancellationToken)
            ?? throw new NotFoundException($"Document {request.DocumentId} not found.");

        if (document.DriverId != request.DriverId)
            throw new NotFoundException($"Document {request.DocumentId} does not belong to driver {request.DriverId}.");

        var fileResult = await fileStorageService.RetrieveAsync(document.StorageKey, cancellationToken);

        return new DriverDocumentFileResult(fileResult.FileName, fileResult.ContentType, fileResult.Data);
    }
}
