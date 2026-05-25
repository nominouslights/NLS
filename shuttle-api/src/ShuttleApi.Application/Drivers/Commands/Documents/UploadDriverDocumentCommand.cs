using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Documents;

public sealed record UploadDriverDocumentCommand(
    Guid DriverId,
    DocumentType DocumentType,
    string FileName,
    string ContentType,
    byte[] FileData,
    DateTime? ExpiryDate,
    // Drug & Alcohol Test
    DateTime? TestDate,
    TestResult? TestResultValue,
    string? TestedBy,
    // Driver's License
    string? LicenseNumber,
    LicenseClass? LicenseClass,
    DateTime? IssuedDate,
    string? LicenseProvince,
    // Police Record Check
    CheckResult? CheckResultValue,
    string? IssuingAuthority,
    // Driver Abstract
    int? ViolationCount,
    int? AtFaultAccidentCount,
    string? Notes) : ICommand<UploadDriverDocumentResult>;

public sealed record UploadDriverDocumentResult(Guid DocumentId);
