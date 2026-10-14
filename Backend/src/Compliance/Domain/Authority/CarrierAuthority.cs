using NorthernLink.Compliance.Domain.Authority.Events;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Authority;

/// <summary>
/// The carrier's operating authority — one per tenant (NL-SP-02 ss. 1–2): its NSC number, the
/// Safety Fitness Certificate in force and every one before it, the safety rating, the
/// intra-provincial declaration and the routes it runs.
/// <para>
/// <b>The NSC number lives here.</b> <c>TenantComplianceSettings.NscNumber</c> is the print-header
/// copy, updated by the <c>RecordNscNumber</c> handler in the same save; settings refuse a
/// different number once an authority exists.
/// </para>
/// <para>
/// <b>An SFC is an R20 record.</b> Recording a new number or expiry moves the current certificate
/// into <see cref="SfcHistory"/> and raises an event the mapper turns into a record-produced
/// integration event naming the old certificate's source id as the one it supersedes, so the
/// registry starts the old record's 2-year "superseded copies" clock.
/// </para>
/// </summary>
public sealed class CarrierAuthority : AggregateRoot, ITenantScoped
{
    public const int NscNumberMaxLength = TenantComplianceSettings.NscNumberMaxLength;
    public const int OperatingRoutesMaxLength = 20_000;

    private CarrierAuthority()
    {
        // EF Core materialization only.
        SfcHistory = [];
    }

    public Guid TenantId { get; private set; }
    public string? NscNumber { get; private set; }
    public SfcCertificate? CurrentSfc { get; private set; }

    /// <summary>Superseded certificates, oldest first, each with its <c>SupersededOn</c> date.</summary>
    public IReadOnlyList<SfcCertificate> SfcHistory { get; private set; }

    public SafetyRating SafetyRating { get; private set; }
    public DateOnly? SafetyRatingAsOf { get; private set; }

    /// <summary>The carrier operates within Manitoba only (Trips enforces it on every stop). True from the start; the declaration signs to it.</summary>
    public bool IntraProvincial { get; private set; }

    public DateOnly? IntraProvincialDeclaredOn { get; private set; }
    public SignatureBlock? IntraProvincialSignature { get; private set; }

    /// <summary>The routes the carrier runs, as markdown — printed into plan s. 1.</summary>
    public string? OperatingRoutes { get; private set; }

    public Guid? ModifiedBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>The tenant's empty authority — unaudited, intra-provincial, nothing on file yet.</summary>
    public static CarrierAuthority Open(Guid tenantId, Guid? actorId, DateTimeOffset now)
    {
        var authority = new CarrierAuthority
        {
            TenantId = tenantId,
            SafetyRating = SafetyRating.Unaudited,
            IntraProvincial = true,
            SfcHistory = [],
            ModifiedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        authority.Raise(new CarrierAuthorityOpenedDomainEvent(authority.Id, tenantId, actorId));
        return authority;
    }

    /// <summary>
    /// Records the SFC in force (HTA s. 312.2(1)). The same number and expiry again is a silent
    /// success (a corrected issue date is applied in place); anything else is a new certificate —
    /// the current one moves to history, superseded today.
    /// </summary>
    public Result RecordSfc(string? number, DateOnly issuedOn, DateOnly expiresOn, Guid? actorId, DateTimeOffset now)
    {
        var trimmed = number?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > SfcCertificate.NumberMaxLength)
        {
            return Result.Failure(CarrierAuthorityErrors.SfcNumberRequired);
        }

        if (expiresOn <= issuedOn)
        {
            return Result.Failure(CarrierAuthorityErrors.SfcExpiryNotAfterIssue);
        }

        if (CurrentSfc is { } current
            && string.Equals(current.Number, trimmed, StringComparison.OrdinalIgnoreCase)
            && current.ExpiresOn == expiresOn)
        {
            if (current.IssuedOn == issuedOn)
            {
                return Result.Success();
            }

            CurrentSfc = current with { IssuedOn = issuedOn };
            Touch(actorId, now);
            Raise(new SfcIssueDateCorrectedDomainEvent(Id, current.CertificateId, current.IssuedOn, issuedOn, actorId));
            return Result.Success();
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var superseded = CurrentSfc is { } previous ? previous with { SupersededOn = today } : null;
        if (superseded is not null)
        {
            SfcHistory = [.. SfcHistory, superseded];
        }

        var certificate = SfcCertificate.New(trimmed, issuedOn, expiresOn);
        CurrentSfc = certificate;
        Touch(actorId, now);
        Raise(new SafetyFitnessCertificateRecordedDomainEvent(Id, TenantId, certificate, superseded?.SourceRecordId, actorId));
        return Result.Success();
    }

    /// <summary>Records the carrier's NSC number (M.R. 132/2024 s. 9). Returns true when it changed.</summary>
    public Result<bool> RecordNscNumber(string? nscNumber, Guid? actorId, DateTimeOffset now)
    {
        var trimmed = nscNumber?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > NscNumberMaxLength)
        {
            return Result.Failure<bool>(CarrierAuthorityErrors.NscNumberRequired);
        }

        if (trimmed == NscNumber)
        {
            return Result.Success(false);
        }

        var previous = NscNumber;
        NscNumber = trimmed;
        Touch(actorId, now);
        Raise(new NscNumberRecordedDomainEvent(Id, previous, trimmed, actorId));
        return Result.Success(true);
    }

    /// <summary>Records the rating the department assigned and the date it applies from.</summary>
    public Result SetSafetyRating(SafetyRating rating, DateOnly? asOf, Guid? actorId, DateTimeOffset now)
    {
        if (!Enum.IsDefined(rating))
        {
            return Result.Failure(CarrierAuthorityErrors.SafetyRatingUnknown);
        }

        if (asOf is not { } date)
        {
            return Result.Failure(CarrierAuthorityErrors.SafetyRatingDateRequired);
        }

        if (rating == SafetyRating && date == SafetyRatingAsOf)
        {
            return Result.Success();
        }

        SafetyRating = rating;
        SafetyRatingAsOf = date;
        Touch(actorId, now);
        Raise(new SafetyRatingSetDomainEvent(Id, rating, date, actorId));
        return Result.Success();
    }

    /// <summary>
    /// The owner signs that the carrier operates within Manitoba only (NL-SP-02 s. 1 and the
    /// Declaration). Only an Owner signature is accepted. Signing again replaces the declaration —
    /// the aggregate snapshots keep every earlier one.
    /// </summary>
    public Result DeclareIntraProvincial(DateOnly declaredOn, SignatureBlock signature, Guid? actorId, DateTimeOffset now)
    {
        if (signature.Role != SignatoryRole.Owner)
        {
            return Result.Failure(CarrierAuthorityErrors.IntraProvincialSignerMustBeOwner);
        }

        if (declaredOn > DateOnly.FromDateTime(now.UtcDateTime).AddDays(1))
        {
            // One day of slack: a Winnipeg evening is already tomorrow in UTC.
            return Result.Failure(CarrierAuthorityErrors.DeclarationDateInFuture);
        }

        IntraProvincial = true;
        IntraProvincialDeclaredOn = declaredOn;
        IntraProvincialSignature = signature;
        Touch(actorId, now);
        Raise(new IntraProvincialDeclaredDomainEvent(Id, declaredOn, signature.SignerName, actorId));
        return Result.Success();
    }

    /// <summary>Replaces the routes markdown; blank clears it.</summary>
    public Result SetOperatingRoutes(string? markdown, Guid? actorId, DateTimeOffset now)
    {
        var value = string.IsNullOrWhiteSpace(markdown) ? null : markdown.Trim();
        if (value?.Length > OperatingRoutesMaxLength)
        {
            return Result.Failure(CarrierAuthorityErrors.OperatingRoutesTooLong);
        }

        if (value == OperatingRoutes)
        {
            return Result.Success();
        }

        OperatingRoutes = value;
        Touch(actorId, now);
        Raise(new OperatingRoutesSetDomainEvent(Id, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Attaches (or replaces) the scan of the <b>current</b> certificate. A superseded certificate's
    /// file is evidence already kept for its retention and is never replaced, so only the
    /// certificate in force accepts one.
    /// </summary>
    public Result AttachSfcCertificate(Guid certificateId, AttachmentRef attachment, Guid? actorId, DateTimeOffset now)
    {
        if (CurrentSfc is not { } current || current.CertificateId != certificateId)
        {
            return Result.Failure(CarrierAuthorityErrors.SfcNotCurrent);
        }

        CurrentSfc = current with { Attachment = attachment };
        Touch(actorId, now);
        Raise(new SfcCertificateAttachedDomainEvent(Id, certificateId, attachment.StorageKey, actorId));
        return Result.Success();
    }

    /// <summary>The certificate (current or superseded) with <paramref name="certificateId"/>, or null.</summary>
    public SfcCertificate? FindSfc(Guid certificateId) =>
        CurrentSfc is { } current && current.CertificateId == certificateId
            ? current
            : SfcHistory.FirstOrDefault(c => c.CertificateId == certificateId);

    /// <summary>
    /// Stamps the registry's answer on the certificate it registered (current or historical).
    /// Returns false when no certificate carries <paramref name="sourceRecordId"/> or it already
    /// carries this stamp — replays of the registered event are silent.
    /// </summary>
    public bool StampRecord(string sourceRecordId, Guid recordId, string documentNumber)
    {
        if (CurrentSfc is { } current && current.SourceRecordId == sourceRecordId)
        {
            if (current.ComplianceRecordId == recordId && current.DocumentNumber == documentNumber)
            {
                return false;
            }

            CurrentSfc = current with { ComplianceRecordId = recordId, DocumentNumber = documentNumber };
        }
        else
        {
            var index = SfcHistory.ToList().FindIndex(c => c.SourceRecordId == sourceRecordId);
            if (index < 0 || (SfcHistory[index].ComplianceRecordId == recordId && SfcHistory[index].DocumentNumber == documentNumber))
            {
                return false;
            }

            var history = SfcHistory.ToList();
            history[index] = history[index] with { ComplianceRecordId = recordId, DocumentNumber = documentNumber };
            SfcHistory = history;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new AuthorityRecordStampedDomainEvent(Id, sourceRecordId, recordId, documentNumber));
        return true;
    }

    private void Touch(Guid? actorId, DateTimeOffset now)
    {
        ModifiedBy = actorId;
        UpdatedAtUtc = now;
    }
}
