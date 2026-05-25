using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Queries.Drivers;

internal sealed class GetDriverByIdQueryHandler(IDriverRepository driverRepository)
    : IRequestHandler<GetDriverByIdQuery, DriverDetailResult>
{
    public async Task<DriverDetailResult> Handle(
        GetDriverByIdQuery request,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdWithDocumentsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Driver {request.Id} not found.");

        var documentResults = driver.Documents.Select(MapDocument).ToList();

        return new DriverDetailResult(
            driver.Id,
            driver.EmployeeId,
            driver.FirstName,
            driver.LastName,
            driver.FullName,
            driver.Phone,
            driver.Email,
            driver.HireDate,
            driver.Status.ToString(),
            driver.IsActive,
            driver.CreatedAt,
            documentResults);
    }

    private static DriverDocumentResult MapDocument(DriverDocument doc) => new(
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
        doc.Notes);
}
