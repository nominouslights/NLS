using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Signatures;

/// <summary>
/// A pointer to a file this module holds in object storage — an SFC, an insurance certificate, a
/// filing confirmation, a registration — the Drivers credential-image precedent generalised. The
/// bytes live in <c>IObjectStorage</c> under <see cref="StorageKey"/>; the row keeps what a person
/// needs to see and download it. A value: replacing a file is a new <see cref="AttachmentRef"/>
/// (and a new key), so the superseded object stays where it was for the record's retention.
/// </summary>
public sealed record AttachmentRef(
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    Guid? UploadedBy)
{
    public const int StorageKeyMaxLength = 512;
    public const int FileNameMaxLength = 255;
    public const long MaxSizeBytes = 15L * 1024 * 1024;

    /// <summary>What the plan's paperwork arrives as: a scan or a photo.</summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/heic"] = ".heic",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png", ".heic" };

    /// <summary>The upload gate, run before a byte is stored: a non-empty file of an allowed type within the size limit.</summary>
    public static Result Validate(string? contentType, long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            return Result.Failure(AttachmentErrors.FileRequired);
        }

        if (sizeBytes > MaxSizeBytes)
        {
            return Result.Failure(AttachmentErrors.TooLarge);
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.ContainsKey(contentType.Trim()))
        {
            return Result.Failure(AttachmentErrors.ContentTypeNotAllowed);
        }

        return Result.Success();
    }

    public static Result<AttachmentRef> Create(
        string? storageKey,
        string? fileName,
        string? contentType,
        long sizeBytes,
        DateTimeOffset uploadedAtUtc,
        Guid? uploadedBy)
    {
        var valid = Validate(contentType, sizeBytes);
        if (valid.IsFailure)
        {
            return Result.Failure<AttachmentRef>(valid.Error);
        }

        var key = storageKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > StorageKeyMaxLength || !key.StartsWith(KeyRoot, StringComparison.Ordinal))
        {
            return Result.Failure<AttachmentRef>(AttachmentErrors.StorageKeyInvalid);
        }

        var name = SafeFileName(fileName);
        if (name.Length == 0)
        {
            name = "attachment" + AllowedContentTypes[contentType!.Trim()];
        }

        return Result.Success(new AttachmentRef(key, name, contentType!.Trim().ToLowerInvariant(), sizeBytes, uploadedAtUtc, uploadedBy));
    }

    /// <summary>The first segment of every key this module writes.</summary>
    public const string KeyRoot = "compliance/";

    /// <summary>
    /// <c>compliance/{tenantId}/{ownerKind}/{ownerId}/{slot}/{guid}{ext}</c>. A fresh guid per
    /// upload, so a replacement never overwrites the file it replaces. The extension comes from the
    /// uploaded file name when it is one of the allowed ones, else from the content type.
    /// </summary>
    public static string BuildKey(Guid tenantId, string ownerKind, Guid ownerId, string slot, string? fileName, string contentType)
    {
        var extension = Path.GetExtension(SafeFileName(fileName));
        if (!AllowedExtensions.Contains(extension))
        {
            extension = AllowedContentTypes.TryGetValue(contentType.Trim(), out var mapped) ? mapped : ".bin";
        }

        return $"{KeyRoot}{tenantId:D}/{ownerKind}/{ownerId:D}/{slot}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }

    private static string SafeFileName(string? fileName)
    {
        // Browsers may send a full client path; keep the last segment only, whatever the separator.
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..].Trim();
        return name.Length > FileNameMaxLength ? name[^FileNameMaxLength..] : name;
    }
}

/// <summary>
/// The attachment slots each owner kind offers. A slot is a stable lowercase path segment — it
/// appears in the upload route, the download route and the storage key.
/// </summary>
public static class AttachmentSlots
{
    public const string Certificate = "certificate";
    public const string FilingConfirmation = "filing-confirmation";
    public const string Document = "document";
}

/// <summary>Every refusal of the upload and download paths — static, so the basis coverage test sees them.</summary>
public static class AttachmentErrors
{
    public static readonly Error FileRequired = ComplianceError.Validation(
        "Compliance.Attachment.FileRequired", "Choose a file to upload — the request carried no file, or an empty one.",
        BasisKeys.Hta318_8_1, BasisKeys.Plan11);

    public static readonly Error TooLarge = ComplianceError.Validation(
        "Compliance.Attachment.TooLarge", $"The file must be {AttachmentRef.MaxSizeBytes / 1024 / 1024} MB or smaller.",
        BasisKeys.Plan11);

    public static readonly Error ContentTypeNotAllowed = ComplianceError.Validation(
        "Compliance.Attachment.ContentTypeNotAllowed", "Only PDF, JPEG, PNG and HEIC files can be attached.",
        BasisKeys.Plan11);

    public static readonly Error StorageKeyInvalid = ComplianceError.Validation(
        "Compliance.Attachment.StorageKeyInvalid", "The attachment's storage key is not one this module issued.",
        BasisKeys.Plan11);

    public static readonly Error SlotUnknown = ComplianceError.Validation(
        "Compliance.Attachment.SlotUnknown", "That attachment slot does not exist for this document.",
        BasisKeys.Plan11);

    public static readonly Error NotAttached = ComplianceError.NotFound(
        "Compliance.Attachment.NotAttached", "No file has been attached in that slot yet.",
        BasisKeys.Hta318_8_2, BasisKeys.Plan11);

    public static readonly Error StorageUnavailable = ComplianceError.Conflict(
        "Compliance.Attachment.StorageUnavailable", "The file store could not be reached. Nothing was saved — please try again.",
        BasisKeys.Hta318_8_2, BasisKeys.Plan11);
}
