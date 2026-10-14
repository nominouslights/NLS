using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Registrations.Events;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Registrations;

/// <summary>
/// A vehicle's registration (plate) term — the Autopac renewal the PMVI schedule is aligned to
/// (NL-SP-02 s. 5.3; M.R. 76/94 s. 3(2)(a)(i)) and an R20 record ("insurance certificates and
/// registration"). One <see cref="AuthorityDocumentStatus.Current"/> registration per vehicle;
/// <see cref="Renew"/> supersedes. Without an explicit expiry the term defaults to the rule set's
/// <c>authority.registrationTermMonths</c> (6) from the effective date; the class defaults to
/// <see cref="DefaultVehicleClass"/>.
/// </summary>
public sealed class VehicleRegistration : AggregateRoot, ITenantScoped
{
    public const int PlateMaxLength = 16;
    public const int VehicleClassMaxLength = 8;
    public const int UnitNumberMaxLength = 32;
    public const int MaxTermMonths = 36;
    public const string DefaultVehicleClass = "AR";

    private VehicleRegistration()
    {
        // EF Core materialization only.
        VehicleUnitNumber = null!;
        Plate = null!;
        VehicleClass = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string VehicleUnitNumber { get; private set; }
    public string Plate { get; private set; }
    public string VehicleClass { get; private set; }
    public int TermMonths { get; private set; }
    public DateOnly EffectiveOn { get; private set; }
    public DateOnly ExpiresOn { get; private set; }
    public AttachmentRef? Document { get; private set; }
    public AuthorityDocumentStatus Status { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public Guid? SupersededById { get; private set; }
    public DateOnly? SupersededOn { get; private set; }
    public Guid? ComplianceRecordId { get; private set; }
    public string? DocumentNumber { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string SourceRecordId => ComplianceSourceIds.VehicleRegistration(Id);

    public static Result<VehicleRegistration> Record(
        Guid tenantId,
        Guid vehicleId,
        string vehicleUnitNumber,
        VehicleRegistrationDetails details,
        int defaultTermMonths,
        Guid? actorId,
        DateTimeOffset now) =>
        Build(tenantId, vehicleId, vehicleUnitNumber, details, defaultTermMonths, supersedes: null, actorId, now);

    /// <summary>The renewed term: a new Current registration, this one Superseded today.</summary>
    public Result<VehicleRegistration> Renew(VehicleRegistrationDetails details, int defaultTermMonths, Guid? actorId, DateTimeOffset now)
    {
        if (Status != AuthorityDocumentStatus.Current)
        {
            return Result.Failure<VehicleRegistration>(RegistrationErrors.NotCurrent);
        }

        // A renewal keeps the plate and class unless the person typed new ones.
        var carried = details with
        {
            Plate = string.IsNullOrWhiteSpace(details.Plate) ? Plate : details.Plate,
            VehicleClass = string.IsNullOrWhiteSpace(details.VehicleClass) ? VehicleClass : details.VehicleClass,
        };

        var successor = Build(TenantId, VehicleId, VehicleUnitNumber, carried, defaultTermMonths, supersedes: this, actorId, now);
        if (successor.IsFailure)
        {
            return successor;
        }

        Status = AuthorityDocumentStatus.Superseded;
        SupersededById = successor.Value.Id;
        SupersededOn = DateOnly.FromDateTime(now.UtcDateTime);
        Touch(actorId, now);
        Raise(new VehicleRegistrationSupersededDomainEvent(Id, successor.Value.Id, SupersededOn.Value, actorId));
        return successor;
    }

    /// <summary>Attaches (or replaces) the registration document — the only slot is <c>document</c>.</summary>
    public Result Attach(string slot, AttachmentRef attachment, Guid? actorId, DateTimeOffset now)
    {
        if (slot != AttachmentSlots.Document)
        {
            return Result.Failure(AttachmentErrors.SlotUnknown);
        }

        Document = attachment;
        Touch(actorId, now);
        Raise(new VehicleRegistrationAttachmentSetDomainEvent(Id, slot, attachment.StorageKey, actorId));
        return Result.Success();
    }

    public Result<AttachmentRef?> AttachmentIn(string slot) =>
        slot == AttachmentSlots.Document
            ? Result.Success(Document)
            : Result.Failure<AttachmentRef?>(AttachmentErrors.SlotUnknown);

    public bool StampRecord(Guid recordId, string documentNumber)
    {
        if (ComplianceRecordId == recordId && DocumentNumber == documentNumber)
        {
            return false;
        }

        ComplianceRecordId = recordId;
        DocumentNumber = documentNumber;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new VehicleRegistrationRecordStampedDomainEvent(Id, recordId, documentNumber));
        return true;
    }

    private static Result<VehicleRegistration> Build(
        Guid tenantId,
        Guid vehicleId,
        string vehicleUnitNumber,
        VehicleRegistrationDetails details,
        int defaultTermMonths,
        VehicleRegistration? supersedes,
        Guid? actorId,
        DateTimeOffset now)
    {
        var plate = details.Plate?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(plate) || plate.Length > PlateMaxLength)
        {
            return Result.Failure<VehicleRegistration>(RegistrationErrors.PlateRequired);
        }

        var vehicleClass = string.IsNullOrWhiteSpace(details.VehicleClass)
            ? DefaultVehicleClass
            : details.VehicleClass.Trim().ToUpperInvariant();
        if (vehicleClass.Length > VehicleClassMaxLength)
        {
            return Result.Failure<VehicleRegistration>(RegistrationErrors.VehicleClassTooLong);
        }

        var term = details.TermMonths ?? defaultTermMonths;
        if (term is < 1 or > MaxTermMonths)
        {
            return Result.Failure<VehicleRegistration>(RegistrationErrors.TermMonthsInvalid);
        }

        if (details.EffectiveOn is not { } effective)
        {
            return Result.Failure<VehicleRegistration>(RegistrationErrors.EffectiveOnRequired);
        }

        var expires = details.ExpiresOn ?? effective.AddMonths(term);
        if (expires <= effective)
        {
            return Result.Failure<VehicleRegistration>(RegistrationErrors.ExpiryNotAfterEffective);
        }

        var unit = vehicleUnitNumber.Trim();
        var registration = new VehicleRegistration
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            VehicleUnitNumber = unit.Length > UnitNumberMaxLength ? unit[..UnitNumberMaxLength] : unit,
            Plate = plate,
            VehicleClass = vehicleClass,
            TermMonths = term,
            EffectiveOn = effective,
            ExpiresOn = expires,
            Status = AuthorityDocumentStatus.Current,
            SupersedesId = supersedes?.Id,
            ModifiedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        registration.Raise(new VehicleRegistrationRecordedDomainEvent(
            registration.Id, tenantId, vehicleId, registration.VehicleUnitNumber, plate, vehicleClass,
            effective, expires, supersedes?.SourceRecordId, actorId));
        return Result.Success(registration);
    }

    private void Touch(Guid? actorId, DateTimeOffset now)
    {
        ModifiedBy = actorId;
        UpdatedAtUtc = now;
    }
}

/// <summary>Raw input for recording or renewing a registration. Null term/expiry/class take the defaults.</summary>
public sealed record VehicleRegistrationDetails
{
    public string? Plate { get; init; }
    public string? VehicleClass { get; init; }
    public int? TermMonths { get; init; }
    public DateOnly? EffectiveOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
}
