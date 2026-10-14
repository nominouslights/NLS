using NorthernLink.Compliance.Domain.Numbering.Events;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Numbering;

/// <summary>
/// The one settings row per tenant: the carrier's legal name and NSC number (printed in every
/// record header), the document prefix ("NL" in NL-HS-04-2026-0031), the principal place of
/// business (HTA s. 318.8(2) — every record must be accessible there; NSC 9 s. 85(3)(a) — RODS
/// deposited there within 30 days) and a note on where the binders physically are.
/// </summary>
public sealed class TenantComplianceSettings : AggregateRoot, ITenantScoped
{
    public const int CarrierLegalNameMaxLength = 200;
    public const int NscNumberMaxLength = 32;
    public const int DocumentPrefixMaxLength = 6;
    public const int PrincipalPlaceOfBusinessMaxLength = 200;
    public const int FileLocationNoteMaxLength = 1000;

    public const string DefaultCarrierLegalName = "Northern Link Shuttle & Cargo";
    public const string DefaultDocumentPrefix = "NL";
    public const string DefaultPrincipalPlaceOfBusiness = "Leaf Rapids, Manitoba";

    private TenantComplianceSettings()
    {
        // EF Core materialization only.
        CarrierLegalName = null!;
        DocumentPrefix = null!;
        PrincipalPlaceOfBusiness = null!;
    }

    public Guid TenantId { get; private set; }
    public string CarrierLegalName { get; private set; }

    /// <summary>The carrier's NSC number as printed on the SFC; null until the owner enters it (header prints a placeholder).</summary>
    public string? NscNumber { get; private set; }

    public string DocumentPrefix { get; private set; }
    public string PrincipalPlaceOfBusiness { get; private set; }
    public string? FileLocationNote { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>The Northern Link defaults — what seed-defaults writes and what RegisterRecord falls back to for an unseeded tenant.</summary>
    public static TenantComplianceSettings CreateDefault(Guid tenantId, Guid? actorId) =>
        Create(tenantId, new ComplianceSettingsDetails
        {
            CarrierLegalName = DefaultCarrierLegalName,
            DocumentPrefix = DefaultDocumentPrefix,
            PrincipalPlaceOfBusiness = DefaultPrincipalPlaceOfBusiness,
        }, actorId).Value;

    public static Result<TenantComplianceSettings> Create(Guid tenantId, ComplianceSettingsDetails details, Guid? actorId)
    {
        var normalized = Normalize(details);
        if (normalized.IsFailure)
        {
            return Result.Failure<TenantComplianceSettings>(normalized.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var settings = new TenantComplianceSettings
        {
            TenantId = tenantId,
            CarrierLegalName = string.Empty,
            DocumentPrefix = string.Empty,
            PrincipalPlaceOfBusiness = string.Empty,
            ModifiedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        settings.Apply(normalized.Value);
        settings.Raise(new ComplianceSettingsCreatedDomainEvent(settings.Id, tenantId, settings.DocumentPrefix, actorId));
        return Result.Success(settings);
    }

    /// <summary>A full replace (PUT). No change → silent success, no event (the Vendor.Update rule).</summary>
    public Result Update(ComplianceSettingsDetails details, Guid? actorId)
    {
        var normalized = Normalize(details);
        if (normalized.IsFailure)
        {
            return normalized;
        }

        if (normalized.Value == Current())
        {
            return Result.Success();
        }

        Apply(normalized.Value);
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new ComplianceSettingsUpdatedDomainEvent(Id, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Copies the NSC number the carrier authority now holds into the print header. Only the
    /// <c>RecordNscNumber</c> handler calls this, in the same save as the authority change — the
    /// authority is the system of record (see <see cref="ComplianceSettingsErrors.NscNumberManagedByAuthority"/>).
    /// </summary>
    public void CopyNscNumberFromAuthority(string nscNumber, Guid? actorId)
    {
        if (NscNumber == nscNumber)
        {
            return;
        }

        NscNumber = nscNumber;
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new ComplianceSettingsUpdatedDomainEvent(Id, actorId));
    }

    /// <summary>The NSC number <paramref name="details"/> would set, normalised the way <see cref="Update"/> stores it.</summary>
    public static string? NormalizedNscNumber(ComplianceSettingsDetails details) => Trimmed(details.NscNumber);

    private sealed record Normalized(string CarrierLegalName, string? NscNumber, string DocumentPrefix, string PrincipalPlaceOfBusiness, string? FileLocationNote);

    private Normalized Current() => new(CarrierLegalName, NscNumber, DocumentPrefix, PrincipalPlaceOfBusiness, FileLocationNote);

    private void Apply(Normalized n)
    {
        CarrierLegalName = n.CarrierLegalName;
        NscNumber = n.NscNumber;
        DocumentPrefix = n.DocumentPrefix;
        PrincipalPlaceOfBusiness = n.PrincipalPlaceOfBusiness;
        FileLocationNote = n.FileLocationNote;
    }

    private static Result<Normalized> Normalize(ComplianceSettingsDetails details)
    {
        var name = Trimmed(details.CarrierLegalName);
        if (name is null || name.Length > CarrierLegalNameMaxLength)
        {
            return Result.Failure<Normalized>(ComplianceSettingsErrors.CarrierLegalNameRequired);
        }

        var nsc = Trimmed(details.NscNumber);
        if (nsc?.Length > NscNumberMaxLength)
        {
            return Result.Failure<Normalized>(ComplianceSettingsErrors.NscNumberTooLong);
        }

        var prefix = Trimmed(details.DocumentPrefix)?.ToUpperInvariant();
        if (prefix is null || prefix.Length > DocumentPrefixMaxLength || !prefix.All(char.IsAsciiLetterOrDigit))
        {
            return Result.Failure<Normalized>(ComplianceSettingsErrors.DocumentPrefixInvalid);
        }

        var place = Trimmed(details.PrincipalPlaceOfBusiness);
        if (place is null || place.Length > PrincipalPlaceOfBusinessMaxLength)
        {
            return Result.Failure<Normalized>(ComplianceSettingsErrors.PrincipalPlaceOfBusinessRequired);
        }

        var note = Trimmed(details.FileLocationNote);
        if (note?.Length > FileLocationNoteMaxLength)
        {
            return Result.Failure<Normalized>(ComplianceSettingsErrors.FileLocationNoteTooLong);
        }

        return Result.Success(new Normalized(name, nsc, prefix, place, note));
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Everything a person edits on the settings row; raw input the aggregate trims.</summary>
public sealed record ComplianceSettingsDetails
{
    public string? CarrierLegalName { get; init; }
    public string? NscNumber { get; init; }
    public string? DocumentPrefix { get; init; }
    public string? PrincipalPlaceOfBusiness { get; init; }
    public string? FileLocationNote { get; init; }
}

public static class ComplianceSettingsErrors
{
    public static readonly Error NotFound = ComplianceError.NotFound(
        "Compliance.Settings.NotFound",
        "This tenant has no compliance settings yet. POST /api/compliance/seed-defaults creates them.",
        BasisKeys.Hta318_8_2);

    public static readonly Error CarrierLegalNameRequired = ComplianceError.Validation(
        "Compliance.Settings.CarrierLegalNameRequired",
        $"The carrier's legal name is required (up to {TenantComplianceSettings.CarrierLegalNameMaxLength} characters) — it heads every printed record.",
        BasisKeys.Hta314, BasisKeys.Mr132_2024_9);

    public static readonly Error NscNumberTooLong = ComplianceError.Validation(
        "Compliance.Settings.NscNumberTooLong",
        $"The NSC number must be {TenantComplianceSettings.NscNumberMaxLength} characters or fewer.",
        BasisKeys.Mr132_2024_9);

    /// <summary>
    /// The carrier authority owns the NSC number (NL-SP-02 s. 2); the settings row only carries a
    /// copy for print headers. A settings write that would change it is refused — record the
    /// number with <c>POST /api/compliance/authority/nsc-number</c>, which updates both.
    /// </summary>
    public static readonly Error NscNumberManagedByAuthority = ComplianceError.Conflict(
        "Compliance.Settings.NscNumberManagedByAuthority",
        "The NSC number is recorded on the carrier authority. Change it with POST /api/compliance/authority/nsc-number; settings carry the same number for print headers.",
        BasisKeys.Mr132_2024_9, BasisKeys.Plan2);

    public static readonly Error DocumentPrefixInvalid = ComplianceError.Validation(
        "Compliance.Settings.DocumentPrefixInvalid",
        $"The document prefix must be 1 to {TenantComplianceSettings.DocumentPrefixMaxLength} letters or digits (for example NL).",
        BasisKeys.Plan11);

    public static readonly Error PrincipalPlaceOfBusinessRequired = ComplianceError.Validation(
        "Compliance.Settings.PrincipalPlaceOfBusinessRequired",
        "The principal place of business is required — records must be accessible there.",
        BasisKeys.Hta318_8_2, BasisKeys.Nsc9_85_3);

    public static readonly Error FileLocationNoteTooLong = ComplianceError.Validation(
        "Compliance.Settings.FileLocationNoteTooLong",
        $"The file location note must be {TenantComplianceSettings.FileLocationNoteMaxLength} characters or fewer.",
        BasisKeys.Hta318_8_2);
}
