using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Drivers.Queries.Drivers;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Queries.Documents;

internal sealed class GetDriverDocumentsQueryHandler(IDriverRepository driverRepository)
    : IRequestHandler<GetDriverDocumentsQuery, IReadOnlyList<DriverDocumentResult>>
{
    public async Task<IReadOnlyList<DriverDocumentResult>> Handle(
        GetDriverDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdWithDocumentsAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.DriverId} not found.");

        return driver.Documents.Select(doc => new DriverDocumentResult(
            doc.Id,
            doc.DocumentType.ToString(),
            doc.FileName,
            doc.ContentType,
            doc.FileSizeBytes,
            doc.UploadedAt,
            doc.ExpiryDate,
            doc.IsExpiringSoon,
            doc.TestDate,
            doc.TestResultValue?.ToString(),
            doc.TestedBy,
            doc.LicenseNumber,
            doc.LicenseClass?.ToString(),
            doc.IssuedDate,
            doc.LicenseProvince,
            doc.CheckResultValue?.ToString(),
            doc.IssuingAuthority,
            doc.ViolationCount,
            doc.AtFaultAccidentCount,
            doc.Notes)).ToList();
    }
}
