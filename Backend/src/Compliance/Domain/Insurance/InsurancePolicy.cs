using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance.Events;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Insurance;

/// <summary>
/// A vehicle's liability insurance as certified (NL-SP-02 s. 12.1; HTA ss. 312.2(7), 312.4;
/// M.R. 132/2024 s. 7(1)(b)). One <see cref="AuthorityDocumentStatus.Current"/> policy per
/// vehicle (a partial unique index backs it); <see cref="Renew"/> creates the successor and
/// supersedes this one in the same save. Every policy is an R20 record: recording one raises the
/// event the mapper turns into a record-produced integration event (naming the predecessor on a
/// renewal), and the registry's answer is stamped back.
/// <para>
/// <b>A limit below the minimum is saved, not refused.</b> The certificate is a fact the carrier
/// holds whether or not it meets the plan; refusing it would lose the evidence and hide the gap.
/// <see cref="MinimumLiabilityCad"/> snapshots the rule set's minimum at entry so
/// <see cref="IsBelowMinimum"/> stays what it was when recorded, and the warnings layer reports it.
/// </para>
/// </summary>
public sealed class InsurancePolicy : AggregateRoot, ITenantScoped
{
    public const int InsurerMaxLength = 64;
    public const int PolicyNumberMaxLength = 64;
    public const int UnitNumberMaxLength = 32;

    private InsurancePolicy()
    {
        // EF Core materialization only.
        VehicleUnitNumber = null!;
        Insurer = null!;
        PolicyNumber = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }

    /// <summary>The unit number when the policy was recorded (from the vehicle replica) — the record's subject line.</summary>
    public string VehicleUnitNumber { get; private set; }

    public string Insurer { get; private set; }
    public string PolicyNumber { get; private set; }
    public decimal LiabilityLimitCad { get; private set; }

    /// <summary>Cargo coverage on the certificate, or null when it carries none.</summary>
    public decimal? CargoCoverageCad { get; private set; }

    /// <summary>The rule set's <c>insurance.minimumLiabilityCad</c> when this policy was recorded.</summary>
    public decimal MinimumLiabilityCad { get; private set; }

    public DateOnly EffectiveOn { get; private set; }
    public DateOnly ExpiresOn { get; private set; }
    public AttachmentRef? Certificate { get; private set; }

    /// <summary>The insurer's filing confirmation with the department (HTA s. 312.4).</summary>
    public AttachmentRef? FilingConfirmation { get; private set; }

    public AuthorityDocumentStatus Status { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public Guid? SupersededById { get; private set; }
    public DateOnly? SupersededOn { get; private set; }
    public Guid? ComplianceRecordId { get; private set; }
    public string? DocumentNumber { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsBelowMinimum => LiabilityLimitCad < MinimumLiabilityCad;

    public string SourceRecordId => ComplianceSourceIds.InsurancePolicy(Id);

    /// <summary>Records a vehicle's first (or next, when none is current) policy. The handler checks one-Current.</summary>
    public static Result<InsurancePolicy> Record(
        Guid tenantId,
        Guid vehicleId,
        string vehicleUnitNumber,
        InsurancePolicyDetails details,
        InsuranceTerms terms,
        Guid? actorId,
        DateTimeOffset now) =>
        Build(tenantId, vehicleId, vehicleUnitNumber, details, terms, supersedes: null, actorId, now);

    /// <summary>
    /// Records the renewal: a new Current policy for the same vehicle, and this one Superseded
    /// today. Only the current policy can be renewed.
    /// </summary>
    public Result<InsurancePolicy> Renew(InsurancePolicyDetails details, InsuranceTerms terms, Guid? actorId, DateTimeOffset now)
    {
        if (Status != AuthorityDocumentStatus.Current)
        {
            return Result.Failure<InsurancePolicy>(InsuranceErrors.NotCurrent);
        }

        var successor = Build(TenantId, VehicleId, VehicleUnitNumber, details, terms, supersedes: this, actorId, now);
        if (successor.IsFailure)
        {
            return successor;
        }

        Status = AuthorityDocumentStatus.Superseded;
        SupersededById = successor.Value.Id;
        SupersededOn = DateOnly.FromDateTime(now.UtcDateTime);
        Touch(actorId, now);
        Raise(new InsurancePolicySupersededDomainEvent(Id, successor.Value.Id, SupersededOn.Value, actorId));
        return successor;
    }

    /// <summary>Attaches (or replaces) the file in <paramref name="slot"/>: <c>certificate</c> or <c>filing-confirmation</c>.</summary>
    public Result Attach(string slot, AttachmentRef attachment, Guid? actorId, DateTimeOffset now)
    {
        switch (slot)
        {
            case AttachmentSlots.Certificate:
                Certificate = attachment;
                break;
            case AttachmentSlots.FilingConfirmation:
                FilingConfirmation = attachment;
                break;
            default:
                return Result.Failure(AttachmentErrors.SlotUnknown);
        }

        Touch(actorId, now);
        Raise(new InsurancePolicyAttachmentSetDomainEvent(Id, slot, attachment.StorageKey, actorId));
        return Result.Success();
    }

    /// <summary>The file in <paramref name="slot"/> (null when nothing is attached), or SlotUnknown.</summary>
    public Result<AttachmentRef?> AttachmentIn(string slot) => slot switch
    {
        AttachmentSlots.Certificate => Result.Success(Certificate),
        AttachmentSlots.FilingConfirmation => Result.Success(FilingConfirmation),
        _ => Result.Failure<AttachmentRef?>(AttachmentErrors.SlotUnknown),
    };

    /// <summary>Stamps the registry's answer; false when it is already there (a replay).</summary>
    public bool StampRecord(Guid recordId, string documentNumber)
    {
        if (ComplianceRecordId == recordId && DocumentNumber == documentNumber)
        {
            return false;
        }

        ComplianceRecordId = recordId;
        DocumentNumber = documentNumber;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new InsurancePolicyRecordStampedDomainEvent(Id, recordId, documentNumber));
        return true;
    }

    private static Result<InsurancePolicy> Build(
        Guid tenantId,
        Guid vehicleId,
        string vehicleUnitNumber,
        InsurancePolicyDetails details,
        InsuranceTerms terms,
        InsurancePolicy? supersedes,
        Guid? actorId,
        DateTimeOffset now)
    {
        var insurer = string.IsNullOrWhiteSpace(details.Insurer) ? terms.DefaultInsurer : details.Insurer.Trim();
        if (insurer.Length > InsurerMaxLength)
        {
            return Result.Failure<InsurancePolicy>(InsuranceErrors.InsurerTooLong);
        }

        var policyNumber = details.PolicyNumber?.Trim();
        if (string.IsNullOrEmpty(policyNumber) || policyNumber.Length > PolicyNumberMaxLength)
        {
            return Result.Failure<InsurancePolicy>(InsuranceErrors.PolicyNumberRequired);
        }

        if (details.LiabilityLimitCad is not { } limit || limit <= 0)
        {
            return Result.Failure<InsurancePolicy>(InsuranceErrors.LiabilityLimitRequired);
        }

        if (details.CargoCoverageCad is < 0)
        {
            return Result.Failure<InsurancePolicy>(InsuranceErrors.CargoCoverageInvalid);
        }

        if (details.EffectiveOn is not { } effective || details.ExpiresOn is not { } expires)
        {
            return Result.Failure<InsurancePolicy>(InsuranceErrors.DatesRequired);
        }

        if (expires <= effective)
        {
            return Result.Failure<InsurancePolicy>(InsuranceErrors.ExpiryNotAfterEffective);
        }

        var unit = vehicleUnitNumber.Trim();
        var policy = new InsurancePolicy
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            VehicleUnitNumber = unit.Length > UnitNumberMaxLength ? unit[..UnitNumberMaxLength] : unit,
            Insurer = insurer,
            PolicyNumber = policyNumber,
            LiabilityLimitCad = limit,
            CargoCoverageCad = details.CargoCoverageCad,
            MinimumLiabilityCad = terms.MinimumLiabilityCad,
            EffectiveOn = effective,
            ExpiresOn = expires,
            Status = AuthorityDocumentStatus.Current,
            SupersedesId = supersedes?.Id,
            ModifiedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        policy.Raise(new InsurancePolicyRecordedDomainEvent(
            policy.Id, tenantId, vehicleId, policy.VehicleUnitNumber, insurer, policyNumber, limit, policy.IsBelowMinimum,
            effective, expires, supersedes?.SourceRecordId, actorId));
        return Result.Success(policy);
    }

    private void Touch(Guid? actorId, DateTimeOffset now)
    {
        ModifiedBy = actorId;
        UpdatedAtUtc = now;
    }
}

/// <summary>Raw input for recording or renewing a policy; every field nullable so a missing one is a readable domain error.</summary>
public sealed record InsurancePolicyDetails
{
    public string? Insurer { get; init; }
    public string? PolicyNumber { get; init; }
    public decimal? LiabilityLimitCad { get; init; }
    public decimal? CargoCoverageCad { get; init; }
    public DateOnly? EffectiveOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
}

/// <summary>The rule-set values a policy is recorded against (<c>insurance.insurer</c>, <c>insurance.minimumLiabilityCad</c>).</summary>
public sealed record InsuranceTerms(string DefaultInsurer, decimal MinimumLiabilityCad);
