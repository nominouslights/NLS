using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Authority.Events;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Insurance.Events;
using NorthernLink.Compliance.Domain.Registrations;
using NorthernLink.Compliance.Domain.Registrations.Events;
using NorthernLink.Compliance.Domain.Signatures;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// The Epic 2 aggregates' invariants: SFC supersession into history, insurance renewal with one
/// Current per vehicle and a below-minimum limit saved (not refused), the registration term
/// default, and the signature/attachment value objects.
/// </summary>
public class AuthorityAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly InsuranceTerms Terms = new("MPI", 2_000_000m);

    private static CarrierAuthority NewAuthority()
    {
        var authority = CarrierAuthority.Open(TestCompliance.TenantId, TestCompliance.ActorId, Now);
        authority.ClearDomainEvents();
        return authority;
    }

    // --- SFC ---------------------------------------------------------------------------------------

    [Fact]
    public void The_first_SFC_becomes_current_and_supersedes_nothing()
    {
        var authority = NewAuthority();

        var result = authority.RecordSfc(" MB-SFC-001 ", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), TestCompliance.ActorId, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("MB-SFC-001", authority.CurrentSfc!.Number);
        Assert.Empty(authority.SfcHistory);
        var recorded = Assert.IsType<SafetyFitnessCertificateRecordedDomainEvent>(Assert.Single(authority.DomainEvents));
        Assert.Null(recorded.SupersedesSourceRecordId);
        Assert.StartsWith(ComplianceSourceIds.SfcPrefix, recorded.Certificate.SourceRecordId);
    }

    [Fact]
    public void A_new_number_moves_the_current_SFC_to_history_and_names_it_as_superseded()
    {
        var authority = NewAuthority();
        authority.RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null, Now);
        var first = authority.CurrentSfc!;
        authority.ClearDomainEvents();

        authority.RecordSfc("MB-SFC-002", new DateOnly(2027, 9, 20), new DateOnly(2028, 9, 20), null, Now);

        var old = Assert.Single(authority.SfcHistory);
        Assert.Equal(first.CertificateId, old.CertificateId);
        Assert.Equal(Today, old.SupersededOn);
        Assert.Equal("MB-SFC-002", authority.CurrentSfc!.Number);
        var recorded = Assert.IsType<SafetyFitnessCertificateRecordedDomainEvent>(Assert.Single(authority.DomainEvents));
        Assert.Equal(first.SourceRecordId, recorded.SupersedesSourceRecordId);
    }

    [Fact]
    public void A_new_expiry_on_the_same_number_is_also_a_new_certificate()
    {
        var authority = NewAuthority();
        authority.RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null, Now);

        authority.RecordSfc("MB-SFC-001", new DateOnly(2027, 10, 1), new DateOnly(2028, 10, 1), null, Now);

        Assert.Single(authority.SfcHistory);
        Assert.Equal(new DateOnly(2028, 10, 1), authority.CurrentSfc!.ExpiresOn);
    }

    [Fact]
    public void The_same_certificate_again_is_silent_and_a_corrected_issue_date_is_applied_in_place()
    {
        var authority = NewAuthority();
        authority.RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null, Now);
        var id = authority.CurrentSfc!.CertificateId;
        authority.ClearDomainEvents();

        authority.RecordSfc("mb-sfc-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null, Now);
        Assert.Empty(authority.DomainEvents);

        authority.RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 2), new DateOnly(2027, 10, 1), null, Now);
        Assert.IsType<SfcIssueDateCorrectedDomainEvent>(Assert.Single(authority.DomainEvents));
        Assert.Equal(id, authority.CurrentSfc!.CertificateId);
        Assert.Equal(new DateOnly(2026, 10, 2), authority.CurrentSfc.IssuedOn);
        Assert.Empty(authority.SfcHistory);
    }

    [Theory]
    [InlineData("2027-10-01", "2027-10-01")]
    [InlineData("2027-10-01", "2026-10-01")]
    public void An_expiry_on_or_before_the_issue_date_is_refused(string issued, string expires)
    {
        var result = NewAuthority().RecordSfc("MB-SFC-001", DateOnly.Parse(issued), DateOnly.Parse(expires), null, Now);

        Assert.Equal(CarrierAuthorityErrors.SfcExpiryNotAfterIssue, result.Error);
    }

    [Fact]
    public void Only_the_current_SFC_takes_a_scan_and_a_superseded_one_keeps_its_file()
    {
        var authority = NewAuthority();
        authority.RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null, Now);
        var first = authority.CurrentSfc!.CertificateId;
        var firstScan = Attachment("sfc", first, AttachmentSlots.Certificate);
        Assert.True(authority.AttachSfcCertificate(first, firstScan, null, Now).IsSuccess);
        Assert.IsType<SfcCertificateAttachedDomainEvent>(authority.DomainEvents.Last());

        authority.RecordSfc("MB-SFC-002", new DateOnly(2027, 9, 20), new DateOnly(2028, 9, 20), null, Now);
        var refused = authority.AttachSfcCertificate(first, Attachment("sfc", first, AttachmentSlots.Certificate), null, Now);

        Assert.Equal(CarrierAuthorityErrors.SfcNotCurrent, refused.Error);
        Assert.Equal(firstScan, authority.FindSfc(first)!.Attachment);
        Assert.Null(authority.CurrentSfc!.Attachment);
        Assert.Null(authority.FindSfc(Guid.NewGuid()));
    }

    [Fact]
    public void The_registry_stamp_lands_on_the_certificate_it_names_current_or_historical_and_a_replay_is_silent()
    {
        var authority = NewAuthority();
        authority.RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null, Now);
        var firstSource = authority.CurrentSfc!.SourceRecordId;
        authority.RecordSfc("MB-SFC-002", new DateOnly(2027, 9, 20), new DateOnly(2028, 9, 20), null, Now);
        var recordId = Guid.NewGuid();

        Assert.True(authority.StampRecord(firstSource, recordId, "NL-SP-R20-2026-0001"));
        Assert.False(authority.StampRecord(firstSource, recordId, "NL-SP-R20-2026-0001"));
        Assert.False(authority.StampRecord("sfc:unknown", recordId, "NL-SP-R20-2026-0001"));

        Assert.Equal("NL-SP-R20-2026-0001", authority.SfcHistory[0].DocumentNumber);
        Assert.Null(authority.CurrentSfc!.DocumentNumber);
    }

    [Fact]
    public void Only_the_owner_signs_the_intra_provincial_declaration()
    {
        var authority = NewAuthority();
        var officer = SignatureBlock.Create(SignatoryRole.ComplianceOfficer, "J. Smith", null, null, null, Today, Now, "We operate within Manitoba only.").Value;
        var owner = SignatureBlock.Create(SignatoryRole.Owner, "Emelio Campbell", TestCompliance.ActorId, null, null, Today, Now, "We operate within Manitoba only.").Value;

        Assert.Equal(CarrierAuthorityErrors.IntraProvincialSignerMustBeOwner, authority.DeclareIntraProvincial(Today, officer, null, Now).Error);
        Assert.True(authority.DeclareIntraProvincial(Today, owner, null, Now).IsSuccess);
        Assert.Equal("We operate within Manitoba only.", authority.IntraProvincialSignature!.Statement);
        Assert.Equal(Today, authority.IntraProvincialDeclaredOn);
    }

    [Fact]
    public void An_unchanged_NSC_number_reports_no_change()
    {
        var authority = NewAuthority();

        Assert.True(authority.RecordNscNumber("MB123456", null, Now).Value);
        authority.ClearDomainEvents();
        Assert.False(authority.RecordNscNumber(" MB123456 ", null, Now).Value);
        Assert.Empty(authority.DomainEvents);
        Assert.Equal(CarrierAuthorityErrors.NscNumberRequired, authority.RecordNscNumber("  ", null, Now).Error);
    }

    // --- Insurance ---------------------------------------------------------------------------------

    private static InsurancePolicyDetails Policy(decimal limit = 2_000_000m, string number = "MPI-1", string effective = "2026-10-01", string expires = "2027-10-01") => new()
    {
        PolicyNumber = number,
        LiabilityLimitCad = limit,
        EffectiveOn = DateOnly.Parse(effective),
        ExpiresOn = DateOnly.Parse(expires),
    };

    [Fact]
    public void A_limit_below_the_minimum_is_saved_and_flagged_not_refused()
    {
        var result = InsurancePolicy.Record(TestCompliance.TenantId, TestCompliance.VehicleId, "101", Policy(limit: 1_000_000m), Terms, null, Now);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsBelowMinimum);
        Assert.Equal(2_000_000m, result.Value.MinimumLiabilityCad);
        Assert.Equal("MPI", result.Value.Insurer);
        Assert.True(Assert.IsType<InsurancePolicyRecordedDomainEvent>(Assert.Single(result.Value.DomainEvents)).BelowMinimum);
    }

    [Fact]
    public void Renewing_supersedes_the_current_policy_and_the_successor_names_it()
    {
        var current = InsurancePolicy.Record(TestCompliance.TenantId, TestCompliance.VehicleId, "101", Policy(), Terms, null, Now).Value;
        current.ClearDomainEvents();

        var successor = current.Renew(Policy(number: "MPI-2", effective: "2027-10-01", expires: "2028-10-01"), Terms, null, Now).Value;

        Assert.Equal(AuthorityDocumentStatus.Superseded, current.Status);
        Assert.Equal(successor.Id, current.SupersededById);
        Assert.Equal(Today, current.SupersededOn);
        Assert.Equal(AuthorityDocumentStatus.Current, successor.Status);
        Assert.Equal(current.Id, successor.SupersedesId);
        Assert.Equal(current.VehicleId, successor.VehicleId);
        var recorded = Assert.IsType<InsurancePolicyRecordedDomainEvent>(Assert.Single(successor.DomainEvents));
        Assert.Equal(current.SourceRecordId, recorded.SupersedesSourceRecordId);
        Assert.IsType<InsurancePolicySupersededDomainEvent>(Assert.Single(current.DomainEvents));
    }

    [Fact]
    public void A_superseded_policy_cannot_be_renewed_again()
    {
        var current = InsurancePolicy.Record(TestCompliance.TenantId, TestCompliance.VehicleId, "101", Policy(), Terms, null, Now).Value;
        current.Renew(Policy(number: "MPI-2"), Terms, null, Now);

        Assert.Equal(InsuranceErrors.NotCurrent, current.Renew(Policy(number: "MPI-3"), Terms, null, Now).Error);
    }

    [Theory]
    [InlineData(null, 2_000_000, "2026-10-01", "2027-10-01", "Compliance.Insurance.PolicyNumberRequired")]
    [InlineData("MPI-1", 0, "2026-10-01", "2027-10-01", "Compliance.Insurance.LiabilityLimitRequired")]
    [InlineData("MPI-1", 2_000_000, "2027-10-01", "2027-10-01", "Compliance.Insurance.ExpiryNotAfterEffective")]
    public void Invalid_policy_details_are_refused(string? number, int limit, string effective, string expires, string code)
    {
        var details = new InsurancePolicyDetails
        {
            PolicyNumber = number, LiabilityLimitCad = limit, EffectiveOn = DateOnly.Parse(effective), ExpiresOn = DateOnly.Parse(expires),
        };

        var result = InsurancePolicy.Record(TestCompliance.TenantId, TestCompliance.VehicleId, "101", details, Terms, null, Now);

        Assert.Equal(code, result.Error.Code);
    }

    [Fact]
    public void Attachments_go_to_their_slot_and_an_unknown_slot_is_refused()
    {
        var policy = InsurancePolicy.Record(TestCompliance.TenantId, TestCompliance.VehicleId, "101", Policy(), Terms, null, Now).Value;
        var file = Attachment("insurance-policies", policy.Id, AttachmentSlots.FilingConfirmation);

        Assert.True(policy.Attach(AttachmentSlots.FilingConfirmation, file, null, Now).IsSuccess);
        Assert.Equal(file, policy.AttachmentIn(AttachmentSlots.FilingConfirmation).Value);
        Assert.Null(policy.AttachmentIn(AttachmentSlots.Certificate).Value);
        Assert.Equal(AttachmentErrors.SlotUnknown, policy.Attach(AttachmentSlots.Document, file, null, Now).Error);
    }

    // --- Registration ------------------------------------------------------------------------------

    [Fact]
    public void A_registration_without_an_expiry_runs_the_default_term_in_class_AR()
    {
        var result = VehicleRegistration.Record(
            TestCompliance.TenantId, TestCompliance.VehicleId, "101",
            new VehicleRegistrationDetails { Plate = " ab 123 ", EffectiveOn = new DateOnly(2026, 10, 1) },
            defaultTermMonths: 6, null, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2027, 4, 1), result.Value.ExpiresOn);
        Assert.Equal(6, result.Value.TermMonths);
        Assert.Equal("AR", result.Value.VehicleClass);
        Assert.Equal("AB 123", result.Value.Plate);
    }

    [Fact]
    public void Renewing_a_registration_carries_the_plate_and_supersedes_the_old_term()
    {
        var current = VehicleRegistration.Record(
            TestCompliance.TenantId, TestCompliance.VehicleId, "101",
            new VehicleRegistrationDetails { Plate = "AB123", EffectiveOn = new DateOnly(2026, 10, 1) }, 6, null, Now).Value;

        var successor = current.Renew(new VehicleRegistrationDetails { EffectiveOn = new DateOnly(2027, 4, 1) }, 6, null, Now).Value;

        Assert.Equal("AB123", successor.Plate);
        Assert.Equal(new DateOnly(2027, 10, 1), successor.ExpiresOn);
        Assert.Equal(AuthorityDocumentStatus.Superseded, current.Status);
        var recorded = Assert.IsType<VehicleRegistrationRecordedDomainEvent>(Assert.Single(successor.DomainEvents));
        Assert.Equal(current.SourceRecordId, recorded.SupersedesSourceRecordId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    public void A_term_outside_1_to_36_months_is_refused(int term)
    {
        var result = VehicleRegistration.Record(
            TestCompliance.TenantId, TestCompliance.VehicleId, "101",
            new VehicleRegistrationDetails { Plate = "AB123", TermMonths = term, EffectiveOn = new DateOnly(2026, 10, 1) }, 6, null, Now);

        Assert.Equal(RegistrationErrors.TermMonthsInvalid, result.Error);
    }

    // --- Value objects -----------------------------------------------------------------------------

    [Theory]
    [InlineData("application/pdf", 1_000, true)]
    [InlineData("image/jpeg", 1_000, true)]
    [InlineData("image/png", 1_000, true)]
    [InlineData("image/heic", 1_000, true)]
    [InlineData("IMAGE/PNG", 1_000, true)]
    [InlineData("image/gif", 1_000, false)]
    [InlineData("application/zip", 1_000, false)]
    [InlineData(null, 1_000, false)]
    [InlineData("application/pdf", 0, false)]
    [InlineData("application/pdf", AttachmentRef.MaxSizeBytes, true)]
    [InlineData("application/pdf", AttachmentRef.MaxSizeBytes + 1, false)]
    public void The_upload_gate_admits_pdf_and_images_up_to_15_MB(string? contentType, long size, bool admitted) =>
        Assert.Equal(admitted, AttachmentRef.Validate(contentType, size).IsSuccess);

    [Fact]
    public void The_upload_gate_names_the_reason()
    {
        Assert.Equal(AttachmentErrors.FileRequired, AttachmentRef.Validate("application/pdf", 0).Error);
        Assert.Equal(AttachmentErrors.TooLarge, AttachmentRef.Validate("application/pdf", AttachmentRef.MaxSizeBytes + 1).Error);
        Assert.Equal(AttachmentErrors.ContentTypeNotAllowed, AttachmentRef.Validate("text/html", 10).Error);
    }

    [Fact]
    public void Storage_keys_follow_the_compliance_pattern_with_a_fresh_name_per_upload()
    {
        var owner = Guid.NewGuid();

        var first = AttachmentRef.BuildKey(TestCompliance.TenantId, "insurance-policies", owner, "certificate", @"C:\scans\Cert.PDF", "application/pdf");
        var second = AttachmentRef.BuildKey(TestCompliance.TenantId, "insurance-policies", owner, "certificate", "photo", "image/jpeg");

        Assert.Matches(
            $"^compliance/{TestCompliance.TenantId:D}/insurance-policies/{owner:D}/certificate/[0-9a-f]{{32}}\\.pdf$", first);
        Assert.EndsWith(".jpg", second, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void An_attachment_keeps_only_the_file_name_and_refuses_a_key_this_module_did_not_issue()
    {
        var ok = AttachmentRef.Create("compliance/t/insurance-policies/o/certificate/x.pdf", @"C:\Users\me\Cert.pdf", "application/pdf", 10, Now, null);
        var foreign = AttachmentRef.Create("driver-credentials/x.pdf", "Cert.pdf", "application/pdf", 10, Now, null);

        Assert.Equal("Cert.pdf", ok.Value.FileName);
        Assert.Equal(AttachmentErrors.StorageKeyInvalid, foreign.Error);
    }

    [Fact]
    public void A_signature_keeps_the_statement_verbatim_and_needs_a_name_and_a_statement()
    {
        const string statement = "  I declare that Northern Link operates within Manitoba only.\n";

        var signed = SignatureBlock.Create(SignatoryRole.Owner, " Emelio Campbell ", null, " ", null, Today, Now, statement);

        Assert.Equal(statement, signed.Value.Statement);
        Assert.Equal("Emelio Campbell", signed.Value.SignerName);
        Assert.Null(signed.Value.Phone);
        Assert.Equal(SignatureErrors.SignerNameRequired, SignatureBlock.Create(SignatoryRole.Owner, " ", null, null, null, Today, Now, statement).Error);
        Assert.Equal(SignatureErrors.StatementRequired, SignatureBlock.Create(SignatoryRole.Owner, "E", null, null, null, Today, Now, "  ").Error);
    }

    [Theory]
    [InlineData(10, null, 11, false)]
    [InlineData(11, null, 11, true)]
    [InlineData(15, false, 11, false)]
    [InlineData(7, true, 11, true)]
    public void A_vehicle_is_regulated_from_eleven_seats_unless_overridden(int seating, bool? regulatedOverride, int threshold, bool regulated) =>
        Assert.Equal(regulated, VehicleRegulation.IsRegulated(seating, regulatedOverride, threshold));

    [Theory]
    [InlineData("Active", true)]
    [InlineData("InMaintenance", true)]
    [InlineData("OutOfService", true)]
    [InlineData("Retired", false)]
    [InlineData("Sold", false)]
    [InlineData("Recycled", false)]
    public void Retired_sold_and_recycled_vehicles_leave_readiness(string status, bool counts) =>
        Assert.Equal(counts, VehicleRegulation.CountsForReadiness(status));

    internal static AttachmentRef Attachment(string ownerKind, Guid ownerId, string slot) =>
        AttachmentRef.Create(
            AttachmentRef.BuildKey(TestCompliance.TenantId, ownerKind, ownerId, slot, "file.pdf", "application/pdf"),
            "file.pdf", "application/pdf", 1_024, Now, TestCompliance.ActorId).Value;
}
