using NorthernLink.Compliance.Domain.Records;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// One worked example per row of the NL-SP-02 s. 11 table, run through the catalogue's own rule
/// so the test proves the table as coded, not a copy of it. Every "later of" row is exercised on
/// both arms and on the pending case where the later anchor is still unknown.
/// </summary>
public class RetentionCalculatorTests
{
    private static RetentionOutcome Compute(ComplianceRecordType type, params (AnchorKind Kind, DateOnly Date)[] anchors) =>
        RetentionCalculator.Compute(ComplianceRecordCatalogue.For(type).Rule, TestCompliance.Anchors(anchors));

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void R1_RODS_six_months_after_receipt()
    {
        var outcome = Compute(ComplianceRecordType.R1, (AnchorKind.ReceivedDate, D(2026, 3, 15)));
        Assert.Equal(D(2026, 9, 15), outcome.RetainUntil);
        Assert.False(outcome.Pending);
    }

    [Fact]
    public void R1_is_pending_until_the_carrier_receives_the_record()
    {
        var outcome = Compute(ComplianceRecordType.R1, (AnchorKind.RecordDate, D(2026, 3, 1)));
        Assert.True(outcome.Pending);
        Assert.Null(outcome.RetainUntil);
    }

    [Fact]
    public void R2_exempt_time_records_six_months_from_record_date() =>
        Assert.Equal(D(2026, 7, 31), Compute(ComplianceRecordType.R2, (AnchorKind.RecordDate, D(2026, 1, 31))).RetainUntil);

    [Fact]
    public void R3_unidentified_driver_profiles_six_months_after_receipt() =>
        Assert.Equal(D(2026, 8, 1), Compute(ComplianceRecordType.R3, (AnchorKind.ReceivedDate, D(2026, 2, 1))).RetainUntil);

    [Fact]
    public void R4_ELD_malfunction_entries_six_months_after_repair()
    {
        Assert.Equal(D(2026, 11, 10), Compute(ComplianceRecordType.R4, (AnchorKind.RepairDate, D(2026, 5, 10))).RetainUntil);
        Assert.True(Compute(ComplianceRecordType.R4, (AnchorKind.RecordDate, D(2026, 5, 1))).Pending);
    }

    [Fact]
    public void R5_notices_of_non_compliance_two_years() =>
        Assert.Equal(D(2028, 4, 1), Compute(ComplianceRecordType.R5, (AnchorKind.RecordDate, D(2026, 4, 1))).RetainUntil);

    [Fact]
    public void R6_ELD_packet_current_version_is_pending_and_superseded_keeps_six_months()
    {
        Assert.True(Compute(ComplianceRecordType.R6, (AnchorKind.RecordDate, D(2026, 1, 1))).Pending);
        Assert.Equal(D(2026, 12, 30), Compute(ComplianceRecordType.R6, (AnchorKind.Superseded, D(2026, 6, 30))).RetainUntil);
    }

    [Fact]
    public void R7_monthly_HOS_audit_two_years_from_month_end() =>
        Assert.Equal(D(2028, 1, 31), Compute(ComplianceRecordType.R7, (AnchorKind.MonthEnd, D(2026, 1, 31))).RetainUntil);

    [Fact]
    public void Month_end_is_derived_from_the_record_date_when_a_producer_sends_only_that()
    {
        var derived = RetentionCalculator.WithDerivedAnchors(TestCompliance.Anchors((AnchorKind.RecordDate, D(2026, 2, 10))));

        Assert.Equal(D(2026, 2, 28), derived[AnchorKind.MonthEnd]);
        Assert.Equal(D(2028, 2, 28), RetentionCalculator.Compute(ComplianceRecordCatalogue.For(ComplianceRecordType.R7).Rule, derived).RetainUntil);
    }

    [Fact]
    public void An_explicit_month_end_is_never_overwritten_by_derivation()
    {
        var derived = RetentionCalculator.WithDerivedAnchors(
            TestCompliance.Anchors((AnchorKind.RecordDate, D(2026, 2, 10)), (AnchorKind.MonthEnd, D(2026, 3, 31))));

        Assert.Equal(D(2026, 3, 31), derived[AnchorKind.MonthEnd]);
    }

    [Fact]
    public void R8_monthly_compliance_report_two_years_from_month_end() =>
        Assert.Equal(D(2028, 3, 31), Compute(ComplianceRecordType.R8, (AnchorKind.MonthEnd, D(2026, 3, 31))).RetainUntil);

    [Fact]
    public void R9_trip_inspection_reports_month_created_plus_six_months() =>
        // 31 August + 6 months lands on the last day of February.
        Assert.Equal(D(2027, 2, 28), Compute(ComplianceRecordType.R9, (AnchorKind.MonthEnd, D(2026, 8, 31))).RetainUntil);

    [Fact]
    public void R10_maintenance_records_later_of_two_years_and_six_months_after_the_vehicle_leaves()
    {
        // Arm 1 wins: the vehicle stays for years, so two years from the record is the earlier date.
        var vehicleLeavesLate = Compute(ComplianceRecordType.R10,
            (AnchorKind.RecordDate, D(2026, 1, 15)), (AnchorKind.VehicleLeftControl, D(2027, 12, 1)));
        Assert.Equal(D(2028, 6, 1), vehicleLeavesLate.RetainUntil);

        // Arm 2 wins: the vehicle left early, two years from the record is later.
        var vehicleLeavesEarly = Compute(ComplianceRecordType.R10,
            (AnchorKind.RecordDate, D(2026, 1, 15)), (AnchorKind.VehicleLeftControl, D(2026, 3, 1)));
        Assert.Equal(D(2028, 1, 15), vehicleLeavesEarly.RetainUntil);
    }

    [Fact]
    public void R10_is_pending_while_the_vehicle_is_still_in_the_fleet()
    {
        var outcome = Compute(ComplianceRecordType.R10, (AnchorKind.RecordDate, D(2020, 1, 1)));
        Assert.True(outcome.Pending);
        Assert.Null(outcome.RetainUntil);
    }

    [Fact]
    public void R11_PMVI_certificates_later_of_supersession_and_two_years()
    {
        Assert.Equal(D(2026, 9, 1), Compute(ComplianceRecordType.R11,
            (AnchorKind.Superseded, D(2026, 9, 1)), (AnchorKind.RecordDate, D(2024, 3, 1))).RetainUntil);
        Assert.Equal(D(2026, 3, 1), Compute(ComplianceRecordType.R11,
            (AnchorKind.Superseded, D(2024, 9, 1)), (AnchorKind.RecordDate, D(2024, 3, 1))).RetainUntil);
        Assert.True(Compute(ComplianceRecordType.R11, (AnchorKind.RecordDate, D(2024, 3, 1))).Pending);
    }

    [Fact]
    public void R12_CVSA_reports_two_years() =>
        Assert.Equal(D(2028, 2, 14), Compute(ComplianceRecordType.R12, (AnchorKind.RecordDate, D(2026, 2, 14))).RetainUntil);

    [Fact]
    public void R13_recall_work_two_years_after_completion()
    {
        Assert.Equal(D(2028, 7, 4), Compute(ComplianceRecordType.R13, (AnchorKind.CompletionDate, D(2026, 7, 4))).RetainUntil);
        Assert.True(Compute(ComplianceRecordType.R13, (AnchorKind.RecordDate, D(2026, 7, 4))).Pending);
    }

    [Fact]
    public void R14_driver_abstracts_and_reviews_two_years() =>
        Assert.Equal(D(2028, 5, 5), Compute(ComplianceRecordType.R14, (AnchorKind.RecordDate, D(2026, 5, 5))).RetainUntil);

    [Fact]
    public void R15_licence_disclosure_until_employment_ends()
    {
        Assert.Equal(D(2027, 3, 31), Compute(ComplianceRecordType.R15, (AnchorKind.EmploymentEnd, D(2027, 3, 31))).RetainUntil);
        Assert.True(Compute(ComplianceRecordType.R15, (AnchorKind.RecordDate, D(2026, 1, 1))).Pending);
    }

    [Fact]
    public void R16_training_records_employment_end_plus_two_years() =>
        Assert.Equal(D(2029, 3, 31), Compute(ComplianceRecordType.R16, (AnchorKind.EmploymentEnd, D(2027, 3, 31))).RetainUntil);

    [Fact]
    public void R17_disciplinary_records_until_employment_ends() =>
        Assert.Equal(D(2027, 3, 31), Compute(ComplianceRecordType.R17, (AnchorKind.EmploymentEnd, D(2027, 3, 31))).RetainUntil);

    [Fact]
    public void R18_bills_of_lading_and_manifests_two_years() =>
        Assert.Equal(D(2028, 10, 1), Compute(ComplianceRecordType.R18, (AnchorKind.RecordDate, D(2026, 10, 1))).RetainUntil);

    [Fact]
    public void R19_incident_files_later_of_two_years_and_claim_closed()
    {
        Assert.Equal(D(2028, 6, 15), Compute(ComplianceRecordType.R19,
            (AnchorKind.RecordDate, D(2026, 1, 10)), (AnchorKind.ClaimClosed, D(2028, 6, 15))).RetainUntil);
        Assert.Equal(D(2028, 1, 10), Compute(ComplianceRecordType.R19,
            (AnchorKind.RecordDate, D(2026, 1, 10)), (AnchorKind.ClaimClosed, D(2026, 5, 1))).RetainUntil);
        Assert.True(Compute(ComplianceRecordType.R19, (AnchorKind.RecordDate, D(2026, 1, 10))).Pending);
    }

    [Fact]
    public void R20_insurance_superseded_copies_two_years()
    {
        Assert.Equal(D(2028, 4, 1), Compute(ComplianceRecordType.R20, (AnchorKind.Superseded, D(2026, 4, 1))).RetainUntil);
        Assert.True(Compute(ComplianceRecordType.R20, (AnchorKind.RecordDate, D(2026, 4, 1))).Pending);
    }

    [Fact]
    public void R21_safety_plan_is_permanent_and_never_pending()
    {
        var outcome = Compute(ComplianceRecordType.R21, (AnchorKind.RecordDate, D(2020, 1, 1)));
        Assert.True(outcome.Permanent);
        Assert.False(outcome.Pending);
        Assert.Null(outcome.RetainUntil);
    }

    [Fact]
    public void The_catalogue_covers_every_record_type_exactly_once()
    {
        var types = Enum.GetValues<ComplianceRecordType>();
        Assert.Equal(21, types.Length);
        Assert.Equal(types.OrderBy(t => t), ComplianceRecordCatalogue.All.Select(d => d.Type));
        Assert.All(ComplianceRecordCatalogue.All, d => Assert.NotEmpty(d.RegulatoryBasisKeys));
    }

    [Theory]
    [InlineData(ComplianceRecordType.R9, "03")]
    [InlineData(ComplianceRecordType.R5, "04")]
    [InlineData(ComplianceRecordType.R15, "02")]
    [InlineData(ComplianceRecordType.R11, "R11")]
    public void Form_numbers_come_from_the_primary_form_code_or_fall_back_to_the_R_code(ComplianceRecordType type, string expected) =>
        Assert.Equal(expected, ComplianceRecordCatalogue.For(type).FormNumber);
}
