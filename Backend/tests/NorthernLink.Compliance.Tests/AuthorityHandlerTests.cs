using NorthernLink.Compliance.Application.Authority;
using NorthernLink.Compliance.Application.Insurance;
using NorthernLink.Compliance.Application.Registrations;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Application.Settings;
using NorthernLink.Compliance.Application.Vehicles;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Registrations;
using NorthernLink.Compliance.Domain.Signatures;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// The Epic 2 handlers over the in-memory unit of work: the NSC number's handover from settings to
/// the authority (both rows in one save; settings refuse a different number afterwards), the
/// one-Current rule for insurance and registration, renewal, and the regulated override.
/// </summary>
public class AuthorityHandlerTests
{
    private readonly InMemoryComplianceStore _store = new();
    private readonly InMemoryLookupStore _lookups = new();
    private readonly InMemoryReferenceStore _reference = new();
    private readonly InMemoryRuleSetApplicationRepository _applications = new();

    private ActiveRuleSetResolver Rules => new(_reference, _applications);

    private static ComplianceSettingsDetails SettingsWith(string? nsc) => new()
    {
        CarrierLegalName = "Northern Link Shuttle & Cargo",
        DocumentPrefix = "NL",
        PrincipalPlaceOfBusiness = "Leaf Rapids, Manitoba",
        NscNumber = nsc,
    };

    // --- NSC number --------------------------------------------------------------------------------

    [Fact]
    public async Task Recording_the_NSC_number_updates_the_authority_and_the_settings_header_in_one_save()
    {
        var handler = new RecordNscNumberCommandHandler(_store, _store, _store);

        var result = await handler.Handle(new RecordNscNumberCommand(TestCompliance.TenantId, " MB123456 ", TestCompliance.ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _store.SaveAttempts);
        Assert.Equal("MB123456", Assert.Single(_store.Authorities).NscNumber);
        var settings = Assert.Single(_store.Settings);
        Assert.Equal("MB123456", settings.NscNumber);
        Assert.Equal(TenantComplianceSettings.DefaultDocumentPrefix, settings.DocumentPrefix);
    }

    [Fact]
    public async Task Once_the_authority_holds_the_NSC_number_settings_refuse_a_different_one_but_accept_the_same()
    {
        await new RecordNscNumberCommandHandler(_store, _store, _store)
            .Handle(new RecordNscNumberCommand(TestCompliance.TenantId, "MB123456", null), CancellationToken.None);
        var update = new UpdateComplianceSettingsCommandHandler(_store, _store, _store);

        var different = await update.Handle(new UpdateComplianceSettingsCommand(TestCompliance.TenantId, SettingsWith("MB999999"), null), CancellationToken.None);
        var cleared = await update.Handle(new UpdateComplianceSettingsCommand(TestCompliance.TenantId, SettingsWith(null), null), CancellationToken.None);
        var same = await update.Handle(new UpdateComplianceSettingsCommand(TestCompliance.TenantId, SettingsWith(" MB123456 ") with { FileLocationNote = "Binder shelf B" }, null), CancellationToken.None);

        Assert.Equal(ComplianceSettingsErrors.NscNumberManagedByAuthority, different.Error);
        Assert.Equal(ComplianceSettingsErrors.NscNumberManagedByAuthority, cleared.Error);
        Assert.True(same.IsSuccess);
        Assert.Equal("Binder shelf B", Assert.Single(_store.Settings).FileLocationNote);
        Assert.Equal("MB123456", _store.Settings[0].NscNumber);
    }

    [Fact]
    public async Task Before_the_authority_holds_an_NSC_number_settings_still_edit_it()
    {
        // An authority opened by recording an SFC, with no NSC number yet.
        await new RecordSfcCommandHandler(_store, _store).Handle(
            new RecordSfcCommand(TestCompliance.TenantId, "MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null), CancellationToken.None);

        var result = await new UpdateComplianceSettingsCommandHandler(_store, _store, _store)
            .Handle(new UpdateComplianceSettingsCommand(TestCompliance.TenantId, SettingsWith("MB555"), null), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Recording_an_SFC_without_dates_is_refused_and_opens_nothing()
    {
        var result = await new RecordSfcCommandHandler(_store, _store)
            .Handle(new RecordSfcCommand(TestCompliance.TenantId, "MB-SFC-001", null, new DateOnly(2027, 10, 1), null), CancellationToken.None);

        Assert.Equal(CarrierAuthorityErrors.SfcDatesRequired, result.Error);
        Assert.Empty(_store.Authorities);
    }

    [Fact]
    public async Task The_authority_reads_as_an_empty_first_run_state_until_something_is_recorded()
    {
        var query = new GetCarrierAuthorityQueryHandler(_store);
        var today = new DateOnly(2026, 10, 12);

        var before = await query.Handle(new GetCarrierAuthorityQuery(TestCompliance.TenantId, today), CancellationToken.None);
        await new RecordSfcCommandHandler(_store, _store).Handle(
            new RecordSfcCommand(TestCompliance.TenantId, "MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null), CancellationToken.None);
        var after = await query.Handle(new GetCarrierAuthorityQuery(TestCompliance.TenantId, today), CancellationToken.None);

        Assert.False(before.Value.Exists);
        Assert.True(before.Value.IntraProvincial);
        Assert.Equal(SafetyRating.Unaudited, before.Value.SafetyRating);
        Assert.True(after.Value.Exists);
        Assert.True(after.Value.CurrentSfc!.ValidToday);
        Assert.Null(after.Value.CurrentSfc.DocumentNumber);
    }

    [Fact]
    public async Task The_declaration_handler_stamps_the_signer_and_refuses_a_missing_statement()
    {
        var handler = new DeclareIntraProvincialCommandHandler(_store, _store);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var missing = await handler.Handle(new DeclareIntraProvincialCommand(
            TestCompliance.TenantId, today, new SignatureInput(SignatoryRole.Owner, "Emelio Campbell", null, null, " ", null), TestCompliance.ActorId), CancellationToken.None);
        var signed = await handler.Handle(new DeclareIntraProvincialCommand(
            TestCompliance.TenantId, today, new SignatureInput(SignatoryRole.Owner, "Emelio Campbell", null, null, "Manitoba only.", null), TestCompliance.ActorId), CancellationToken.None);

        Assert.Equal(SignatureErrors.StatementRequired, missing.Error);
        Assert.True(signed.IsSuccess);
        var signature = Assert.Single(_store.Authorities).IntraProvincialSignature!;
        Assert.Equal(TestCompliance.ActorId, signature.SignerUserId);
        Assert.Equal(today, signature.SignedOn);
    }

    // --- Insurance ---------------------------------------------------------------------------------

    private static InsurancePolicyDetails Policy(string number = "MPI-1", decimal limit = 2_000_000m) => new()
    {
        PolicyNumber = number,
        LiabilityLimitCad = limit,
        EffectiveOn = new DateOnly(2026, 10, 1),
        ExpiresOn = new DateOnly(2027, 10, 1),
    };

    [Fact]
    public async Task A_policy_needs_the_vehicle_in_the_replica()
    {
        var result = await new RecordInsurancePolicyCommandHandler(_store, _lookups, Rules, _store)
            .Handle(new RecordInsurancePolicyCommand(TestCompliance.TenantId, TestCompliance.VehicleId, Policy(), null), CancellationToken.None);

        Assert.Equal(ComplianceVehicleErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task A_second_policy_for_a_vehicle_with_a_current_one_is_refused_and_renewal_keeps_one_current()
    {
        _lookups.AddVehicle(TestCompliance.VehicleId, unit: "101");
        var record = new RecordInsurancePolicyCommandHandler(_store, _lookups, Rules, _store);

        var first = await record.Handle(new RecordInsurancePolicyCommand(TestCompliance.TenantId, TestCompliance.VehicleId, Policy(limit: 1_500_000m), null), CancellationToken.None);
        var second = await record.Handle(new RecordInsurancePolicyCommand(TestCompliance.TenantId, TestCompliance.VehicleId, Policy("MPI-2"), null), CancellationToken.None);
        var renewed = await new RenewInsurancePolicyCommandHandler(_store, Rules, _store)
            .Handle(new RenewInsurancePolicyCommand(TestCompliance.TenantId, first.Value, Policy("MPI-2"), null), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(InsuranceErrors.CurrentExists, second.Error);
        Assert.True(renewed.IsSuccess);
        Assert.Equal(2, _store.Policies.Count);
        var current = Assert.Single(_store.Policies, p => p.Status == AuthorityDocumentStatus.Current);
        Assert.Equal(renewed.Value, current.Id);
        Assert.Equal("101", current.VehicleUnitNumber);
        Assert.True(_store.Policies.Single(p => p.Id == first.Value).IsBelowMinimum);

        var listed = await new ListInsurancePoliciesQueryHandler(_store)
            .Handle(new ListInsurancePoliciesQuery(TestCompliance.TenantId, TestCompliance.VehicleId, IncludeSuperseded: false), CancellationToken.None);
        Assert.Equal(renewed.Value, Assert.Single(listed.Value).Id);
    }

    [Fact]
    public async Task An_attachment_slot_without_a_file_is_a_not_found_and_an_attached_one_is_returned()
    {
        _lookups.AddVehicle(TestCompliance.VehicleId);
        var id = (await new RecordInsurancePolicyCommandHandler(_store, _lookups, Rules, _store)
            .Handle(new RecordInsurancePolicyCommand(TestCompliance.TenantId, TestCompliance.VehicleId, Policy(), null), CancellationToken.None)).Value;
        var get = new GetInsuranceAttachmentQueryHandler(_store);

        var empty = await get.Handle(new GetInsuranceAttachmentQuery(TestCompliance.TenantId, id, AttachmentSlots.Certificate), CancellationToken.None);
        var unknown = await get.Handle(new GetInsuranceAttachmentQuery(TestCompliance.TenantId, id, "selfie"), CancellationToken.None);
        var file = AuthorityAggregateTests.Attachment("insurance-policies", id, AttachmentSlots.Certificate);
        await new AttachInsuranceDocumentCommandHandler(_store, _store)
            .Handle(new AttachInsuranceDocumentCommand(TestCompliance.TenantId, id, AttachmentSlots.Certificate, file, null), CancellationToken.None);
        var attached = await get.Handle(new GetInsuranceAttachmentQuery(TestCompliance.TenantId, id, AttachmentSlots.Certificate), CancellationToken.None);

        Assert.Equal(AttachmentErrors.NotAttached, empty.Error);
        Assert.Equal(AttachmentErrors.SlotUnknown, unknown.Error);
        Assert.Equal(file.StorageKey, attached.Value.StorageKey);
    }

    [Fact]
    public async Task An_SFC_scan_attaches_to_the_current_certificate_and_downloads_by_certificate_id()
    {
        var attach = new AttachSfcCertificateCommandHandler(_store, _store);
        var get = new GetSfcAttachmentQueryHandler(_store);
        var nothingYet = await attach.Handle(
            new AttachSfcCertificateCommand(TestCompliance.TenantId, Guid.NewGuid(), AuthorityAggregateTests.Attachment("sfc", Guid.NewGuid(), AttachmentSlots.Certificate), null),
            CancellationToken.None);

        await new RecordSfcCommandHandler(_store, _store).Handle(
            new RecordSfcCommand(TestCompliance.TenantId, "MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null), CancellationToken.None);
        var certificateId = (await new GetCarrierAuthorityQueryHandler(_store)
            .Handle(new GetCarrierAuthorityQuery(TestCompliance.TenantId, new DateOnly(2026, 10, 12)), CancellationToken.None)).Value.CurrentSfc!.CertificateId;
        var notAttached = await get.Handle(new GetSfcAttachmentQuery(TestCompliance.TenantId, certificateId), CancellationToken.None);
        var scan = AuthorityAggregateTests.Attachment("sfc", certificateId, AttachmentSlots.Certificate);
        var attached = await attach.Handle(new AttachSfcCertificateCommand(TestCompliance.TenantId, certificateId, scan, null), CancellationToken.None);
        var downloaded = await get.Handle(new GetSfcAttachmentQuery(TestCompliance.TenantId, certificateId), CancellationToken.None);
        var unknown = await get.Handle(new GetSfcAttachmentQuery(TestCompliance.TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(CarrierAuthorityErrors.SfcNotCurrent, nothingYet.Error);
        Assert.Equal(AttachmentErrors.NotAttached, notAttached.Error);
        Assert.True(attached.IsSuccess);
        Assert.Equal(scan.StorageKey, downloaded.Value.StorageKey);
        Assert.Equal(CarrierAuthorityErrors.SfcNotFound, unknown.Error);
    }

    // --- Registration ------------------------------------------------------------------------------

    [Fact]
    public async Task A_registration_takes_its_default_term_from_the_rule_set_in_force()
    {
        _lookups.AddVehicle(TestCompliance.VehicleId);

        var id = await new RecordVehicleRegistrationCommandHandler(_store, _lookups, Rules, _store).Handle(
            new RecordVehicleRegistrationCommand(TestCompliance.TenantId, TestCompliance.VehicleId,
                new VehicleRegistrationDetails { Plate = "AB123", EffectiveOn = new DateOnly(2026, 10, 1) }, null),
            CancellationToken.None);

        var registration = Assert.Single(_store.Registrations);
        Assert.Equal(id.Value, registration.Id);
        Assert.Equal(AuthorityRuleSectionTests.DefaultTerm, registration.TermMonths);
        Assert.Equal(new DateOnly(2027, 4, 1), registration.ExpiresOn);
    }

    [Fact]
    public async Task A_second_registration_for_a_vehicle_with_a_current_one_is_refused()
    {
        _lookups.AddVehicle(TestCompliance.VehicleId);
        var handler = new RecordVehicleRegistrationCommandHandler(_store, _lookups, Rules, _store);
        var details = new VehicleRegistrationDetails { Plate = "AB123", EffectiveOn = new DateOnly(2026, 10, 1) };

        await handler.Handle(new RecordVehicleRegistrationCommand(TestCompliance.TenantId, TestCompliance.VehicleId, details, null), CancellationToken.None);
        var second = await handler.Handle(new RecordVehicleRegistrationCommand(TestCompliance.TenantId, TestCompliance.VehicleId, details, null), CancellationToken.None);

        Assert.Equal(RegistrationErrors.CurrentExists, second.Error);
    }

    // --- Vehicles ----------------------------------------------------------------------------------

    [Fact]
    public async Task The_regulated_override_is_set_on_a_known_vehicle_and_drives_the_listing()
    {
        _lookups.AddVehicle(TestCompliance.VehicleId, seating: 15);
        var set = new SetVehicleRegulatedCommandHandler(_lookups);

        var unknown = await set.Handle(new SetVehicleRegulatedCommand(TestCompliance.TenantId, Guid.NewGuid(), true, null), CancellationToken.None);
        var before = (await new ListComplianceVehiclesQueryHandler(_lookups, Rules)
            .Handle(new ListComplianceVehiclesQuery(TestCompliance.TenantId, new DateOnly(2026, 10, 12)), CancellationToken.None)).Value;
        await set.Handle(new SetVehicleRegulatedCommand(TestCompliance.TenantId, TestCompliance.VehicleId, false, null), CancellationToken.None);
        var after = (await new ListComplianceVehiclesQueryHandler(_lookups, Rules)
            .Handle(new ListComplianceVehiclesQuery(TestCompliance.TenantId, new DateOnly(2026, 10, 12)), CancellationToken.None)).Value;

        Assert.Equal(ComplianceVehicleErrors.NotFound, unknown.Error);
        Assert.True(Assert.Single(before).Regulated);
        Assert.Equal(11, before[0].RegulatedMinSeatingCapacity);
        Assert.False(Assert.Single(after).Regulated);
        Assert.False(after[0].RegulatedOverride!.Value);
    }
}
