namespace NorthernLink.Compliance.Domain.RuleSets;

/// <summary>
/// The in-code rule sets for Manitoba (jurisdiction <c>CA-MB</c>). Every number here is read off
/// NL-SP-02 or built from the engine; a change to the plan is a new <c>V&lt;date&gt;</c> method
/// and a new published row, never an edit to an old one.
/// </summary>
public static class CaMbRuleSets
{
    public const string Jurisdiction = "CA-MB";

    /// <summary>Every rule set this build knows, newest last. <c>POST rule-sets/publish</c> publishes any not yet in the table.</summary>
    public static IReadOnlyList<RuleSetDocument> All => [V2026_10_01(), V2026_10_12()];

    /// <summary>The newest in-code set — the fallback <c>GetActiveRules</c> answers with when a tenant has applied nothing.</summary>
    public static RuleSetDocument Current => All[^1];

    /// <summary>NL-SP-02 as read on 2026-10-01 (rule-set version 1).</summary>
    public static RuleSetDocument V2026_10_01() => new(
        Jurisdiction,
        Version: 1,
        EffectiveFrom: new DateOnly(2026, 10, 1),

        // Mirror of the engine — display and help only. HosEngine is the law.
        Hos: HosRuleSection.FromEngine(),

        // NL-SP-02 s. 11, built from the catalogue so the table is typed once.
        Retention: RetentionRuleSection.FromCatalogue(),

        // NL-SP-02 s. 5: PMVI every 6 months aligned to the Autopac plate renewal (M.R. 76/94
        // ss. 3(2)(a)(i), 5(2)(a)(i)); SPM&I at 16,000–24,000 km or 600–1,000 engine hours,
        // whichever first — warn at the lower bound, overdue at the upper; seasonal September and
        // April; recall check quarterly; trip inspection every 24 h (NSC 13 s. 4(b)); non-regulated
        // vehicles follow the manufacturer plus an annual safety inspection.
        Inspection: new InspectionRuleSection(
            PmviIntervalMonths: 6,
            PmviAlignedToPlateRenewal: true,
            SpmiKmLowerBound: 16_000,
            SpmiKmUpperBound: 24_000,
            SpmiEngineHoursLowerBound: 600,
            SpmiEngineHoursUpperBound: 1_000,
            SpmiWarnAt: "LowerBound",
            SpmiOverdueAt: "UpperBound",
            SeasonalInspectionMonths: [9, 4],
            RecallCheckIntervalMonths: 3,
            TripInspectionIntervalHours: 24,
            NonRegulatedVehicleSchedule: "Manufacturer",
            NonRegulatedSafetyInspectionIntervalMonths: 12),

        // NL-SP-02 s. 3; HTA s. 318.6 (abstract within 30 days before hire and every 12 months,
        // reviewed within 30 days). Fitness thresholds and first-aid timing are the plan's own.
        DriverQualification: new DriverQualificationRuleSection(
            AbstractWithinDaysBeforeHire: 30,
            AbstractIntervalMonths: 12,
            AbstractReviewWithinDays: 30,
            MaxDemerits: 5,
            ImpairedOrCarelessConvictionLookbackYears: 5,
            SuspensionLookbackYears: 3,
            MaxPreventableCollisions: 2,
            PreventableCollisionLookbackYears: 3,
            MinimumLicenceClass: 4,
            LicenceClassOverPassengerThreshold: 2,
            PassengerThresholdForHigherClass: 24,
            FirstAidWithinMonthsOfFirstTrip: 6,
            FirstAidRenewalMonths: 12,
            FirstAidCourseBookedWithinDaysOfHire: 30,
            ReferencesRequired: 2,
            ReferenceYearsVerified: 3,
            // TODO(CONFIRM-10): the plan's onboarding count of supervised round trips is to be confirmed by the owner.
            SupervisedRoundTripsAtOnboarding: 2),

        // NSC 9 s. 85(1) / (3)(a); NSC 13 ss. 15–16; HTA s. 318.1(3)-(4); NSC 9 s. 78(5); HTA s. 312.4.
        Filing: new FilingRuleSection(
            DriverForwardsRodsDays: 20,
            CarrierFilesRodsDays: 30,
            TripInspectionForwardDays: 20,
            TripInspectionFileDays: 30,
            DirectorAccidentReportDays: 30,
            DriverDisclosureDays: 30,
            EldRepairDays: 14,
            InsuranceChangeNoticeDays: 15,
            DisposalReviewWindowDays: 30),

        // 160 km radius exemption; NSC 9 s. 1 personal conveyance 75 km; 1 h planning margin;
        // 75 kg per passenger load estimate; HTA s. 318.1(3)(a)(iii) $2,000 damage disclosure.
        Thresholds: new ThresholdsRuleSection(
            ExemptionRadiusKm: 160,
            PersonalConveyanceKm: 75,
            PlanningMarginHours: 1,
            PassengerLoadEstimateKg: 75,
            AccidentDamageDisclosureThresholdCad: 2_000m,
            // TODO(CONFIRM-11): the carrier-profile warning line (percent of the intervention threshold) is to be confirmed by the owner.
            CarrierProfileWarningPercent: 85),

        // MPI; M.R. 132/2024 s. 7(1)(b) — $2,000,000 for each bus carrying passengers for compensation; HTA s. 312.4 15-day notice.
        Insurance: new InsuranceRuleSection(
            Insurer: "MPI",
            MinimumLiabilityCad: 2_000_000m,
            ChangeNoticeDays: 15));

    /// <summary>
    /// Version 2 (2026-10-12): every version-1 value unchanged, plus the carrier-authority section
    /// (SFC / insurance / registration renewal warnings, the NET-course window, the regulated-vehicle
    /// seating threshold, plan re-acknowledgement) — MTI new-carrier page; HTA ss. 1(1), 312.2, 318.5.
    /// </summary>
    public static RuleSetDocument V2026_10_12() => V2026_10_01() with
    {
        Version = 2,
        EffectiveFrom = new DateOnly(2026, 10, 12),
        Authority = AuthorityRuleSection.Default,
    };
}
