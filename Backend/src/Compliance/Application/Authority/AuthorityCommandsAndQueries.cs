using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Authority;

/// <summary>The tenant's carrier authority, or its empty state when nothing has been recorded.</summary>
public sealed record GetCarrierAuthorityQuery(Guid TenantId, DateOnly Today) : IQuery<CarrierAuthorityResponse>;

/// <summary>Records the SFC in force; a new number or expiry supersedes the current one (R20).</summary>
public sealed record RecordSfcCommand(Guid TenantId, string? Number, DateOnly? IssuedOn, DateOnly? ExpiresOn, Guid? ActorId) : ICommand;

/// <summary>Records the carrier's NSC number and copies it into the settings print header in the same save.</summary>
public sealed record RecordNscNumberCommand(Guid TenantId, string? NscNumber, Guid? ActorId) : ICommand;

public sealed record SetSafetyRatingCommand(Guid TenantId, SafetyRating Rating, DateOnly? AsOf, Guid? ActorId) : ICommand;

/// <summary>What a person types on a signature line; the handler stamps the time and the signed-in user.</summary>
public sealed record SignatureInput(SignatoryRole Role, string? SignerName, string? Phone, string? Email, string? Statement, string? ImageKey);

public sealed record DeclareIntraProvincialCommand(Guid TenantId, DateOnly? DeclaredOn, SignatureInput Signature, Guid? ActorId) : ICommand;

public sealed record SetOperatingRoutesCommand(Guid TenantId, string? Markdown, Guid? ActorId) : ICommand;

/// <summary>Attaches an already-stored scan to the current SFC <paramref name="CertificateId"/>.</summary>
public sealed record AttachSfcCertificateCommand(Guid TenantId, Guid CertificateId, AttachmentRef Attachment, Guid? ActorId) : ICommand;

/// <summary>The stored scan of SFC <paramref name="CertificateId"/> (current or superseded) — the download streams it.</summary>
public sealed record GetSfcAttachmentQuery(Guid TenantId, Guid CertificateId) : IQuery<AttachmentRef>;

/// <summary>An attachment on the wire — never the storage key.</summary>
public sealed record AttachmentResponse(string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAtUtc)
{
    public static AttachmentResponse? From(AttachmentRef? attachment) =>
        attachment is null ? null : new(attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedAtUtc);
}

public sealed record SignatureResponse(
    SignatoryRole Role, string SignerName, Guid? SignerUserId, string? Phone, string? Email, DateOnly SignedOn, DateTimeOffset SignedAtUtc, string Statement)
{
    public static SignatureResponse? From(SignatureBlock? s) =>
        s is null ? null : new(s.Role, s.SignerName, s.SignerUserId, s.Phone, s.Email, s.SignedOn, s.SignedAtUtc, s.Statement);
}

/// <summary>One SFC. <c>DocumentNumber</c> null = the registry has not numbered it yet ("number pending").</summary>
public sealed record SfcResponse(
    Guid CertificateId,
    string Number,
    DateOnly IssuedOn,
    DateOnly ExpiresOn,
    bool ValidToday,
    AttachmentResponse? Attachment,
    Guid? ComplianceRecordId,
    string? DocumentNumber,
    DateOnly? SupersededOn)
{
    public static SfcResponse From(SfcCertificate c, DateOnly today) => new(
        c.CertificateId, c.Number, c.IssuedOn, c.ExpiresOn, c.IsValidOn(today), AttachmentResponse.From(c.Attachment),
        c.ComplianceRecordId, c.DocumentNumber, c.SupersededOn);
}

/// <summary>
/// GET authority. <c>Exists</c> false = nothing recorded yet (every other field at its default:
/// unaudited, intra-provincial, no SFC) — a 200, not a 404, because "nothing on file" is the
/// first-run state a screen renders, not an error.
/// </summary>
public sealed record CarrierAuthorityResponse(
    bool Exists,
    Guid? Id,
    string? NscNumber,
    SfcResponse? CurrentSfc,
    IReadOnlyList<SfcResponse> SfcHistory,
    SafetyRating SafetyRating,
    DateOnly? SafetyRatingAsOf,
    bool IntraProvincial,
    DateOnly? IntraProvincialDeclaredOn,
    SignatureResponse? IntraProvincialSignature,
    string? OperatingRoutes,
    DateTimeOffset? UpdatedAtUtc)
{
    public static CarrierAuthorityResponse Empty { get; } =
        new(false, null, null, null, [], SafetyRating.Unaudited, null, true, null, null, null, null);

    public static CarrierAuthorityResponse From(CarrierAuthority a, DateOnly today) => new(
        true,
        a.Id,
        a.NscNumber,
        a.CurrentSfc is { } current ? SfcResponse.From(current, today) : null,
        a.SfcHistory.Reverse().Select(c => SfcResponse.From(c, today)).ToList(),
        a.SafetyRating,
        a.SafetyRatingAsOf,
        a.IntraProvincial,
        a.IntraProvincialDeclaredOn,
        SignatureResponse.From(a.IntraProvincialSignature),
        a.OperatingRoutes,
        a.UpdatedAtUtc);
}
