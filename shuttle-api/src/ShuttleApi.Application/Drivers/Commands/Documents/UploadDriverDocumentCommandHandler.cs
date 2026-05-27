using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Documents;

internal sealed class UploadDriverDocumentCommandHandler(
    IDriverRepository driverRepository,
    IFileStorageService fileStorageService)
    : IRequestHandler<UploadDriverDocumentCommand, UploadDriverDocumentResult>
{
    private const int MaxFileSizeBytes = 10 * 1024 * 1024;

    public async Task<UploadDriverDocumentResult> Handle(
        UploadDriverDocumentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileData.Length > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"File size {request.FileData.Length} bytes exceeds the 10 MB limit.");

        var driver = await driverRepository.GetByIdWithDocumentsAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.DriverId} not found.");

        // Store the file bytes via the storage abstraction — returns an opaque key
        var storageKey = await fileStorageService.StoreAsync(
            request.FileName,
            request.ContentType,
            request.FileData,
            cancellationToken);

        var document = DriverDocument.Create(
            request.DriverId,
            request.DocumentType,
            request.FileName,
            request.ContentType,
            storageKey,
            request.FileData.LongLength,
            AsUtc(request.ExpiryDate),
            AsUtc(request.TestDate),
            request.TestResultValue,
            request.TestedBy,
            request.LicenseNumber,
            request.LicenseClass,
            AsUtc(request.IssuedDate),
            request.LicenseProvince,
            request.CheckResultValue,
            request.IssuingAuthority,
            request.ViolationCount,
            request.AtFaultAccidentCount,
            request.Notes);

        driver.AddDocument(document);
        await driverRepository.UpdateAsync(driver, cancellationToken);

        return new UploadDriverDocumentResult(document.Id);
    }

    private static DateTime? AsUtc(DateTime? dt) =>
        dt.HasValue ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : null;
}
