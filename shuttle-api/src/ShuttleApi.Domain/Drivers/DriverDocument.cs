using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Drivers;

public sealed class DriverDocument : Entity<Guid>
{
    public Guid DriverId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>
    /// Opaque reference to the stored file. For the DB implementation this is a GUID string;
    /// for future blob storage this will be a blob URL/path.
    /// </summary>
    public string StorageKey { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public DateTime? ExpiryDate { get; private set; }

    // Drug & Alcohol Test fields
    public DateTime? TestDate { get; private set; }
    public TestResult? TestResultValue { get; private set; }
    public string? TestedBy { get; private set; }

    // Driver's License fields
    public string? LicenseNumber { get; private set; }
    public LicenseClass? LicenseClass { get; private set; }
    public DateTime? IssuedDate { get; private set; }
    public string? LicenseProvince { get; private set; }

    // Police Record Check fields
    public CheckResult? CheckResultValue { get; private set; }
    public string? IssuingAuthority { get; private set; }

    // Driver Abstract fields
    public int? ViolationCount { get; private set; }
    public int? AtFaultAccidentCount { get; private set; }

    // Shared nullable field
    public string? Notes { get; private set; }

    public bool IsExpiringSoon =>
        ExpiryDate.HasValue && ExpiryDate.Value <= DateTime.UtcNow.AddDays(60);

    private DriverDocument() { }

    public static DriverDocument Create(
        Guid driverId,
        DocumentType documentType,
        string fileName,
        string contentType,
        string storageKey,
        long fileSizeBytes,
        DateTime? expiryDate,
        DateTime? testDate,
        TestResult? testResultValue,
        string? testedBy,
        string? licenseNumber,
        LicenseClass? licenseClass,
        DateTime? issuedDate,
        string? licenseProvince,
        CheckResult? checkResultValue,
        string? issuingAuthority,
        int? violationCount,
        int? atFaultAccidentCount,
        string? notes)
    {
        return new DriverDocument
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            DocumentType = documentType,
            FileName = fileName,
            ContentType = contentType,
            StorageKey = storageKey,
            FileSizeBytes = fileSizeBytes,
            UploadedAt = DateTime.UtcNow,
            ExpiryDate = expiryDate,
            TestDate = testDate,
            TestResultValue = testResultValue,
            TestedBy = testedBy,
            LicenseNumber = licenseNumber,
            LicenseClass = licenseClass,
            IssuedDate = issuedDate,
            LicenseProvince = licenseProvince,
            CheckResultValue = checkResultValue,
            IssuingAuthority = issuingAuthority,
            ViolationCount = violationCount,
            AtFaultAccidentCount = atFaultAccidentCount,
            Notes = notes
        };
    }
}
