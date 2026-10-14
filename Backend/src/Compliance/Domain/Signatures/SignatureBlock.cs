using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Signatures;

/// <summary>Who a signature line belongs to on a Safety Plan document — a plan designation, not an auth role.</summary>
public enum SignatoryRole
{
    Owner,
    ComplianceOfficer,
    MaintenanceOfficer,
    Driver,
}

/// <summary>
/// One signed line on a plan document (the intra-provincial declaration, a designation letter, a
/// plan version): the statement <b>verbatim</b> as the signer saw it, the typed name, and when —
/// the Fleet <c>VehicleInspection</c> certification shape. A value: an aggregate holds one and
/// never edits it; signing again is a new block, and whether a second signature is allowed at all
/// is the owning aggregate's rule (set-once evidence fields).
/// <para>
/// <see cref="Statement"/> is kept exactly as signed — not trimmed, not normalised — because what
/// was attested is the evidence; it is only refused when blank. <see cref="ImageKey"/> is an
/// optional object-storage key of a drawn signature.
/// </para>
/// </summary>
public sealed record SignatureBlock(
    SignatoryRole Role,
    string SignerName,
    Guid? SignerUserId,
    string? Phone,
    string? Email,
    DateOnly SignedOn,
    DateTimeOffset SignedAtUtc,
    string Statement,
    string? ImageKey)
{
    public const int SignerNameMaxLength = 128;
    public const int PhoneMaxLength = 32;
    public const int EmailMaxLength = 256;
    public const int StatementMaxLength = 4000;
    public const int ImageKeyMaxLength = AttachmentRef.StorageKeyMaxLength;

    public static Result<SignatureBlock> Create(
        SignatoryRole role,
        string? signerName,
        Guid? signerUserId,
        string? phone,
        string? email,
        DateOnly signedOn,
        DateTimeOffset signedAtUtc,
        string? statement,
        string? imageKey = null)
    {
        if (!Enum.IsDefined(role))
        {
            return Result.Failure<SignatureBlock>(SignatureErrors.RoleUnknown);
        }

        var name = signerName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > SignerNameMaxLength)
        {
            return Result.Failure<SignatureBlock>(SignatureErrors.SignerNameRequired);
        }

        if (string.IsNullOrWhiteSpace(statement) || statement.Length > StatementMaxLength)
        {
            return Result.Failure<SignatureBlock>(SignatureErrors.StatementRequired);
        }

        var trimmedPhone = Optional(phone);
        var trimmedEmail = Optional(email);
        var trimmedImage = Optional(imageKey);
        if (trimmedPhone?.Length > PhoneMaxLength || trimmedEmail?.Length > EmailMaxLength || trimmedImage?.Length > ImageKeyMaxLength)
        {
            return Result.Failure<SignatureBlock>(SignatureErrors.ContactTooLong);
        }

        return Result.Success(new SignatureBlock(
            role, name, signerUserId, trimmedPhone, trimmedEmail, signedOn, signedAtUtc, statement, trimmedImage));
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class SignatureErrors
{
    public static readonly Error RoleUnknown = ComplianceError.Validation(
        "Compliance.Signature.RoleUnknown", "The signatory role must be Owner, ComplianceOfficer, MaintenanceOfficer or Driver.",
        BasisKeys.PlanDeclaration, BasisKeys.PlanDesignations);

    public static readonly Error SignerNameRequired = ComplianceError.Validation(
        "Compliance.Signature.SignerNameRequired",
        $"A signature needs the signer's typed name (up to {SignatureBlock.SignerNameMaxLength} characters).",
        BasisKeys.PlanDeclaration, BasisKeys.Hta318_8_1);

    public static readonly Error StatementRequired = ComplianceError.Validation(
        "Compliance.Signature.StatementRequired",
        $"A signature needs the statement being signed, exactly as shown to the signer (up to {SignatureBlock.StatementMaxLength} characters).",
        BasisKeys.PlanDeclaration, BasisKeys.Hta318_8_1);

    public static readonly Error ContactTooLong = ComplianceError.Validation(
        "Compliance.Signature.ContactTooLong",
        $"The signer's phone must be {SignatureBlock.PhoneMaxLength}, email {SignatureBlock.EmailMaxLength} and signature image key {SignatureBlock.ImageKeyMaxLength} characters or fewer.",
        BasisKeys.PlanDeclaration);
}
