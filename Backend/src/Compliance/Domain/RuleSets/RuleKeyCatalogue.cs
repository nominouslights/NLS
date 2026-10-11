namespace NorthernLink.Compliance.Domain.RuleSets;

/// <summary>
/// Every stable rule key a client may ask <c>GET /api/compliance/rules/{key}</c> for. A constant
/// list rather than something derived at runtime, so adding a value to a section without naming
/// its key here (or vice versa) fails <c>RuleKeyResolutionTests</c> instead of shipping a key
/// nobody can look up. Retention keys are listed per row of the s. 11 table.
/// </summary>
public static class RuleKeyCatalogue
{
    public static readonly string[] Hos =
    [
        "hos.dailyDrivingSeconds", "hos.dailyOnDutySeconds", "hos.shiftDrivingSeconds", "hos.shiftOnDutySeconds",
        "hos.shiftElapsedSeconds", "hos.mandatoryConsecutiveOffSeconds", "hos.dailyOffSeconds", "hos.extraOffSeconds",
        "hos.minOffBlockSeconds", "hos.rest24Seconds", "hos.rest24WindowSeconds", "hos.cycleOnDutySeconds",
        "hos.cycleDays", "hos.cycleResetSeconds", "hos.adverseMaxExtensionSeconds", "hos.deferralMaxSeconds",
        "hos.deferralTwoDayOffMinSeconds", "hos.deferralTwoDayDrivingMaxSeconds", "hos.pcDailyLimitDecikm",
        "hos.futureSkewSeconds",
    ];

    public static readonly string[] Retention =
    [
        "retention.R1.carrierMonths", "retention.R1.anchor",
        "retention.R2.carrierMonths", "retention.R2.anchor",
        "retention.R3.carrierMonths", "retention.R3.anchor",
        "retention.R4.carrierMonths", "retention.R4.anchor",
        "retention.R5.carrierMonths", "retention.R5.anchor",
        "retention.R6.carrierMonths", "retention.R6.anchor",
        "retention.R7.carrierMonths", "retention.R7.anchor",
        "retention.R8.carrierMonths", "retention.R8.anchor",
        "retention.R9.carrierMonths", "retention.R9.anchor",
        "retention.R10.carrierMonths", "retention.R10.anchor", "retention.R10.laterOf.vehicleLeftControlMonths",
        "retention.R11.carrierMonths", "retention.R11.anchor", "retention.R11.laterOf.recordDateMonths",
        "retention.R12.carrierMonths", "retention.R12.anchor",
        "retention.R13.carrierMonths", "retention.R13.anchor",
        "retention.R14.carrierMonths", "retention.R14.anchor",
        "retention.R15.carrierMonths", "retention.R15.anchor",
        "retention.R16.carrierMonths", "retention.R16.anchor",
        "retention.R17.carrierMonths", "retention.R17.anchor",
        "retention.R18.carrierMonths", "retention.R18.anchor",
        "retention.R19.carrierMonths", "retention.R19.anchor", "retention.R19.laterOf.claimClosedMonths",
        "retention.R20.carrierMonths", "retention.R20.anchor",
        "retention.R21.permanent",
    ];

    public static readonly string[] Inspection =
    [
        "inspection.pmviIntervalMonths", "inspection.pmviAlignedToPlateRenewal", "inspection.spmiKmLowerBound",
        "inspection.spmiKmUpperBound", "inspection.spmiEngineHoursLowerBound", "inspection.spmiEngineHoursUpperBound",
        "inspection.spmiWarnAt", "inspection.spmiOverdueAt", "inspection.seasonalInspectionMonths",
        "inspection.recallCheckIntervalMonths", "inspection.tripInspectionIntervalHours",
        "inspection.nonRegulatedVehicleSchedule", "inspection.nonRegulatedSafetyInspectionIntervalMonths",
    ];

    public static readonly string[] DriverQualification =
    [
        "driverQualification.abstractWithinDaysBeforeHire", "driverQualification.abstractIntervalMonths",
        "driverQualification.abstractReviewWithinDays", "driverQualification.maxDemerits",
        "driverQualification.impairedOrCarelessConvictionLookbackYears", "driverQualification.suspensionLookbackYears",
        "driverQualification.maxPreventableCollisions", "driverQualification.preventableCollisionLookbackYears",
        "driverQualification.minimumLicenceClass", "driverQualification.licenceClassOverPassengerThreshold",
        "driverQualification.passengerThresholdForHigherClass", "driverQualification.firstAidWithinMonthsOfFirstTrip",
        "driverQualification.firstAidRenewalMonths", "driverQualification.firstAidCourseBookedWithinDaysOfHire",
        "driverQualification.referencesRequired", "driverQualification.referenceYearsVerified",
        "driverQualification.supervisedRoundTripsAtOnboarding",
    ];

    public static readonly string[] Filing =
    [
        "filing.driverForwardsRodsDays", "filing.carrierFilesRodsDays", "filing.tripInspectionForwardDays",
        "filing.tripInspectionFileDays", "filing.directorAccidentReportDays", "filing.driverDisclosureDays",
        "filing.eldRepairDays", "filing.insuranceChangeNoticeDays", "filing.disposalReviewWindowDays",
    ];

    public static readonly string[] Thresholds =
    [
        "thresholds.exemptionRadiusKm", "thresholds.personalConveyanceKm", "thresholds.planningMarginHours",
        "thresholds.passengerLoadEstimateKg", "thresholds.accidentDamageDisclosureThresholdCad",
        "thresholds.carrierProfileWarningPercent",
    ];

    public static readonly string[] Insurance =
    [
        "insurance.insurer", "insurance.minimumLiabilityCad", "insurance.changeNoticeDays",
    ];

    public static IReadOnlyList<string> All { get; } =
        [.. Hos, .. Retention, .. Inspection, .. DriverQualification, .. Filing, .. Thresholds, .. Insurance];
}
