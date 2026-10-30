using System.Globalization;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.HoursOfService;

namespace NorthernLink.Compliance.Domain.RuleSets;

/// <summary>
/// One rule value, addressable by a stable dotted key (<c>hos.dailyDrivingSeconds</c>,
/// <c>retention.R9.carrierMonths</c>, <c>insurance.minimumLiabilityCad</c>). <see cref="Value"/>
/// is the invariant string form; <see cref="Unit"/> says how to read it ("seconds", "months",
/// "km", "cad", "percent", "days", "hours", "count", "text", "flag").
/// </summary>
public sealed record RuleValue(string Key, string Value, string Unit)
{
    public static RuleValue Of(string key, int value, string unit) => new(key, value.ToString(CultureInfo.InvariantCulture), unit);
    public static RuleValue Of(string key, decimal value, string unit) => new(key, value.ToString(CultureInfo.InvariantCulture), unit);
    public static RuleValue Of(string key, bool value) => new(key, value ? "true" : "false", "flag");
    public static RuleValue Of(string key, string value) => new(key, value, "text");
}

/// <summary>
/// A read-only <b>mirror</b> of <see cref="HosLimits"/> for display and help text. The engine in
/// <c>NorthernLink.Shared.HoursOfService</c> is the law — Drivers and Trips enforce from it bit
/// for bit, and nothing here is ever read by an enforcement path. Built only by
/// <see cref="FromEngine"/>, so the numbers can never be hand-typed into drift.
/// </summary>
public sealed record HosRuleSection(
    int DailyDrivingSeconds,
    int DailyOnDutySeconds,
    int ShiftDrivingSeconds,
    int ShiftOnDutySeconds,
    int ShiftElapsedSeconds,
    int MandatoryConsecutiveOffSeconds,
    int DailyOffSeconds,
    int ExtraOffSeconds,
    int MinOffBlockSeconds,
    int Rest24Seconds,
    int Rest24WindowSeconds,
    int CycleOnDutySeconds,
    int CycleDays,
    int CycleResetSeconds,
    int AdverseMaxExtensionSeconds,
    int DeferralMaxSeconds,
    int DeferralTwoDayOffMinSeconds,
    int DeferralTwoDayDrivingMaxSeconds,
    int PcDailyLimitDecikm,
    int FutureSkewSeconds)
{
    public static HosRuleSection FromEngine() => new(
        HosLimits.DailyDrivingSeconds,
        HosLimits.DailyOnDutySeconds,
        HosLimits.ShiftDrivingSeconds,
        HosLimits.ShiftOnDutySeconds,
        HosLimits.ShiftElapsedSeconds,
        HosLimits.MandatoryConsecutiveOffSeconds,
        HosLimits.DailyOffSeconds,
        HosLimits.ExtraOffSeconds,
        HosLimits.MinOffBlockSeconds,
        HosLimits.Rest24Seconds,
        HosLimits.Rest24WindowSeconds,
        HosLimits.CycleOnDutySeconds,
        HosLimits.CycleDays,
        HosLimits.CycleResetSeconds,
        HosLimits.AdverseMaxExtensionSeconds,
        HosLimits.DeferralMaxSeconds,
        HosLimits.DeferralTwoDayOffMinSeconds,
        HosLimits.DeferralTwoDayDrivingMaxSeconds,
        HosLimits.PcDailyLimitDecikm,
        HosLimits.FutureSkewSeconds);

    public IEnumerable<RuleValue> Flatten()
    {
        yield return RuleValue.Of("hos.dailyDrivingSeconds", DailyDrivingSeconds, "seconds");
        yield return RuleValue.Of("hos.dailyOnDutySeconds", DailyOnDutySeconds, "seconds");
        yield return RuleValue.Of("hos.shiftDrivingSeconds", ShiftDrivingSeconds, "seconds");
        yield return RuleValue.Of("hos.shiftOnDutySeconds", ShiftOnDutySeconds, "seconds");
        yield return RuleValue.Of("hos.shiftElapsedSeconds", ShiftElapsedSeconds, "seconds");
        yield return RuleValue.Of("hos.mandatoryConsecutiveOffSeconds", MandatoryConsecutiveOffSeconds, "seconds");
        yield return RuleValue.Of("hos.dailyOffSeconds", DailyOffSeconds, "seconds");
        yield return RuleValue.Of("hos.extraOffSeconds", ExtraOffSeconds, "seconds");
        yield return RuleValue.Of("hos.minOffBlockSeconds", MinOffBlockSeconds, "seconds");
        yield return RuleValue.Of("hos.rest24Seconds", Rest24Seconds, "seconds");
        yield return RuleValue.Of("hos.rest24WindowSeconds", Rest24WindowSeconds, "seconds");
        yield return RuleValue.Of("hos.cycleOnDutySeconds", CycleOnDutySeconds, "seconds");
        yield return RuleValue.Of("hos.cycleDays", CycleDays, "days");
        yield return RuleValue.Of("hos.cycleResetSeconds", CycleResetSeconds, "seconds");
        yield return RuleValue.Of("hos.adverseMaxExtensionSeconds", AdverseMaxExtensionSeconds, "seconds");
        yield return RuleValue.Of("hos.deferralMaxSeconds", DeferralMaxSeconds, "seconds");
        yield return RuleValue.Of("hos.deferralTwoDayOffMinSeconds", DeferralTwoDayOffMinSeconds, "seconds");
        yield return RuleValue.Of("hos.deferralTwoDayDrivingMaxSeconds", DeferralTwoDayDrivingMaxSeconds, "seconds");
        yield return RuleValue.Of("hos.pcDailyLimitDecikm", PcDailyLimitDecikm, "decikm");
        yield return RuleValue.Of("hos.futureSkewSeconds", FutureSkewSeconds, "seconds");
    }
}

/// <summary>One row of the retention section — the s. 11 table entry as the rule set publishes it.</summary>
public sealed record RetentionRuleEntry(
    string RecordType,
    string DisplayName,
    string Keeper,
    string RegulatoryMinimum,
    string Citation,
    string CarrierRetention,
    RetentionRule Rule);

/// <summary>
/// The 21 retention rules, built FROM <see cref="ComplianceRecordCatalogue"/> by
/// <see cref="FromCatalogue"/> — never typed a second time.
/// </summary>
public sealed record RetentionRuleSection(IReadOnlyList<RetentionRuleEntry> Rules)
{
    public static RetentionRuleSection FromCatalogue() => new(
        ComplianceRecordCatalogue.All
            .Select(d => new RetentionRuleEntry(
                d.Code, d.DisplayName, d.Keeper.ToString(), d.RegulatoryMinimum, d.Citation, d.CarrierRetention, d.Rule))
            .ToList());

    /// <summary>
    /// Keys per row: <c>retention.R9.carrierMonths</c> (the first term's months — the carrier's
    /// primary period), <c>retention.R9.anchor</c> (its anchor), one
    /// <c>retention.R10.laterOf.vehicleLeftControlMonths</c> per additional arm, and
    /// <c>retention.R21.permanent</c> for the permanent row.
    /// </summary>
    public IEnumerable<RuleValue> Flatten()
    {
        foreach (var entry in Rules)
        {
            var prefix = $"retention.{entry.RecordType}";
            if (entry.Rule.IsPermanent)
            {
                yield return RuleValue.Of($"{prefix}.permanent", true);
                continue;
            }

            var primary = entry.Rule.Terms[0];
            yield return RuleValue.Of($"{prefix}.carrierMonths", primary.OffsetMonths, "months");
            yield return RuleValue.Of($"{prefix}.anchor", primary.Anchor.ToString());
            foreach (var term in entry.Rule.Terms.Skip(1))
            {
                yield return RuleValue.Of($"{prefix}.laterOf.{CamelCase(term.Anchor.ToString())}Months", term.OffsetMonths, "months");
            }
        }
    }

    private static string CamelCase(string pascal) => char.ToLowerInvariant(pascal[0]) + pascal[1..];
}

/// <summary>Vehicle inspection cadence (NL-SP-02 s. 5; M.R. 76/94; NSC 11 and 13).</summary>
public sealed record InspectionRuleSection(
    int PmviIntervalMonths,
    bool PmviAlignedToPlateRenewal,
    int SpmiKmLowerBound,
    int SpmiKmUpperBound,
    int SpmiEngineHoursLowerBound,
    int SpmiEngineHoursUpperBound,
    string SpmiWarnAt,
    string SpmiOverdueAt,
    IReadOnlyList<int> SeasonalInspectionMonths,
    int RecallCheckIntervalMonths,
    int TripInspectionIntervalHours,
    string NonRegulatedVehicleSchedule,
    int NonRegulatedSafetyInspectionIntervalMonths)
{
    public IEnumerable<RuleValue> Flatten()
    {
        yield return RuleValue.Of("inspection.pmviIntervalMonths", PmviIntervalMonths, "months");
        yield return RuleValue.Of("inspection.pmviAlignedToPlateRenewal", PmviAlignedToPlateRenewal);
        yield return RuleValue.Of("inspection.spmiKmLowerBound", SpmiKmLowerBound, "km");
        yield return RuleValue.Of("inspection.spmiKmUpperBound", SpmiKmUpperBound, "km");
        yield return RuleValue.Of("inspection.spmiEngineHoursLowerBound", SpmiEngineHoursLowerBound, "hours");
        yield return RuleValue.Of("inspection.spmiEngineHoursUpperBound", SpmiEngineHoursUpperBound, "hours");
        yield return RuleValue.Of("inspection.spmiWarnAt", SpmiWarnAt);
        yield return RuleValue.Of("inspection.spmiOverdueAt", SpmiOverdueAt);
        yield return RuleValue.Of("inspection.seasonalInspectionMonths", string.Join(",", SeasonalInspectionMonths));
        yield return RuleValue.Of("inspection.recallCheckIntervalMonths", RecallCheckIntervalMonths, "months");
        yield return RuleValue.Of("inspection.tripInspectionIntervalHours", TripInspectionIntervalHours, "hours");
        yield return RuleValue.Of("inspection.nonRegulatedVehicleSchedule", NonRegulatedVehicleSchedule);
        yield return RuleValue.Of("inspection.nonRegulatedSafetyInspectionIntervalMonths", NonRegulatedSafetyInspectionIntervalMonths, "months");
    }
}

/// <summary>Driver qualification (NL-SP-02 s. 3; HTA s. 318.6; NSC 4 and 6).</summary>
public sealed record DriverQualificationRuleSection(
    int AbstractWithinDaysBeforeHire,
    int AbstractIntervalMonths,
    int AbstractReviewWithinDays,
    int MaxDemerits,
    int ImpairedOrCarelessConvictionLookbackYears,
    int SuspensionLookbackYears,
    int MaxPreventableCollisions,
    int PreventableCollisionLookbackYears,
    int MinimumLicenceClass,
    int LicenceClassOverPassengerThreshold,
    int PassengerThresholdForHigherClass,
    int FirstAidWithinMonthsOfFirstTrip,
    int FirstAidRenewalMonths,
    int FirstAidCourseBookedWithinDaysOfHire,
    int ReferencesRequired,
    int ReferenceYearsVerified,
    int SupervisedRoundTripsAtOnboarding)
{
    public IEnumerable<RuleValue> Flatten()
    {
        yield return RuleValue.Of("driverQualification.abstractWithinDaysBeforeHire", AbstractWithinDaysBeforeHire, "days");
        yield return RuleValue.Of("driverQualification.abstractIntervalMonths", AbstractIntervalMonths, "months");
        yield return RuleValue.Of("driverQualification.abstractReviewWithinDays", AbstractReviewWithinDays, "days");
        yield return RuleValue.Of("driverQualification.maxDemerits", MaxDemerits, "count");
        yield return RuleValue.Of("driverQualification.impairedOrCarelessConvictionLookbackYears", ImpairedOrCarelessConvictionLookbackYears, "years");
        yield return RuleValue.Of("driverQualification.suspensionLookbackYears", SuspensionLookbackYears, "years");
        yield return RuleValue.Of("driverQualification.maxPreventableCollisions", MaxPreventableCollisions, "count");
        yield return RuleValue.Of("driverQualification.preventableCollisionLookbackYears", PreventableCollisionLookbackYears, "years");
        yield return RuleValue.Of("driverQualification.minimumLicenceClass", MinimumLicenceClass, "class");
        yield return RuleValue.Of("driverQualification.licenceClassOverPassengerThreshold", LicenceClassOverPassengerThreshold, "class");
        yield return RuleValue.Of("driverQualification.passengerThresholdForHigherClass", PassengerThresholdForHigherClass, "count");
        yield return RuleValue.Of("driverQualification.firstAidWithinMonthsOfFirstTrip", FirstAidWithinMonthsOfFirstTrip, "months");
        yield return RuleValue.Of("driverQualification.firstAidRenewalMonths", FirstAidRenewalMonths, "months");
        yield return RuleValue.Of("driverQualification.firstAidCourseBookedWithinDaysOfHire", FirstAidCourseBookedWithinDaysOfHire, "days");
        yield return RuleValue.Of("driverQualification.referencesRequired", ReferencesRequired, "count");
        yield return RuleValue.Of("driverQualification.referenceYearsVerified", ReferenceYearsVerified, "years");
        yield return RuleValue.Of("driverQualification.supervisedRoundTripsAtOnboarding", SupervisedRoundTripsAtOnboarding, "count");
    }
}

/// <summary>Filing and forwarding deadlines (NSC 9 ss. 78, 85; NSC 13 s. 15; HTA ss. 318.1, 312.4).</summary>
public sealed record FilingRuleSection(
    int DriverForwardsRodsDays,
    int CarrierFilesRodsDays,
    int TripInspectionForwardDays,
    int TripInspectionFileDays,
    int DirectorAccidentReportDays,
    int DriverDisclosureDays,
    int EldRepairDays,
    int InsuranceChangeNoticeDays,
    int DisposalReviewWindowDays)
{
    public IEnumerable<RuleValue> Flatten()
    {
        yield return RuleValue.Of("filing.driverForwardsRodsDays", DriverForwardsRodsDays, "days");
        yield return RuleValue.Of("filing.carrierFilesRodsDays", CarrierFilesRodsDays, "days");
        yield return RuleValue.Of("filing.tripInspectionForwardDays", TripInspectionForwardDays, "days");
        yield return RuleValue.Of("filing.tripInspectionFileDays", TripInspectionFileDays, "days");
        yield return RuleValue.Of("filing.directorAccidentReportDays", DirectorAccidentReportDays, "days");
        yield return RuleValue.Of("filing.driverDisclosureDays", DriverDisclosureDays, "days");
        yield return RuleValue.Of("filing.eldRepairDays", EldRepairDays, "days");
        yield return RuleValue.Of("filing.insuranceChangeNoticeDays", InsuranceChangeNoticeDays, "days");
        yield return RuleValue.Of("filing.disposalReviewWindowDays", DisposalReviewWindowDays, "days");
    }
}

/// <summary>Operational thresholds the plan names in passing.</summary>
public sealed record ThresholdsRuleSection(
    int ExemptionRadiusKm,
    int PersonalConveyanceKm,
    int PlanningMarginHours,
    int PassengerLoadEstimateKg,
    decimal AccidentDamageDisclosureThresholdCad,
    int CarrierProfileWarningPercent)
{
    public IEnumerable<RuleValue> Flatten()
    {
        yield return RuleValue.Of("thresholds.exemptionRadiusKm", ExemptionRadiusKm, "km");
        yield return RuleValue.Of("thresholds.personalConveyanceKm", PersonalConveyanceKm, "km");
        yield return RuleValue.Of("thresholds.planningMarginHours", PlanningMarginHours, "hours");
        yield return RuleValue.Of("thresholds.passengerLoadEstimateKg", PassengerLoadEstimateKg, "kg");
        yield return RuleValue.Of("thresholds.accidentDamageDisclosureThresholdCad", AccidentDamageDisclosureThresholdCad, "cad");
        yield return RuleValue.Of("thresholds.carrierProfileWarningPercent", CarrierProfileWarningPercent, "percent");
    }
}

/// <summary>Insurance (NL-SP-02 s. 12; HTA ss. 312.2(7), 312.4; M.R. 132/2024 s. 7(1)(b)).</summary>
public sealed record InsuranceRuleSection(
    string Insurer,
    decimal MinimumLiabilityCad,
    int ChangeNoticeDays)
{
    public IEnumerable<RuleValue> Flatten()
    {
        yield return RuleValue.Of("insurance.insurer", Insurer);
        yield return RuleValue.Of("insurance.minimumLiabilityCad", MinimumLiabilityCad, "cad");
        yield return RuleValue.Of("insurance.changeNoticeDays", ChangeNoticeDays, "days");
    }
}
