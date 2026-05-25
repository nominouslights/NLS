using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Queries.Drivers;

public sealed record GetDriverByIdQuery(Guid Id) : IQuery<DriverDetailResult>;

public sealed record DriverDetailResult(
    Guid Id,
    string EmployeeId,
    string FirstName,
    string LastName,
    string FullName,
    string Phone,
    string Email,
    DateTime HireDate,
    string Status,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<DriverDocumentResult> Documents);

public sealed record DriverDocumentResult(
    Guid Id,
    string DocumentType,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    DateTime? ExpiryDate,
    bool IsExpiringSoon,
    // Structured fields
    DateTime? TestDate,
    string? TestResultValue,
    string? TestedBy,
    string? LicenseNumber,
    string? LicenseClass,
    DateTime? IssuedDate,
    string? LicenseProvince,
    string? CheckResultValue,
    string? IssuingAuthority,
    int? ViolationCount,
    int? AtFaultAccidentCount,
    string? Notes);
