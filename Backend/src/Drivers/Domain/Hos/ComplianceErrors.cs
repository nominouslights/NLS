using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// Domain errors of the compliance aggregates (<see cref="HosException"/>,
/// <see cref="DriverOutOfService"/>, <see cref="EldMalfunctionReport"/>,
/// <see cref="ComplianceFinding"/>) and the driver's prior-records attestation. Codes share the
/// <c>Drivers.Hos.</c> prefix with <see cref="HosErrors"/>; the <c>Forbidden</c> ones are the
/// role checks the handlers run underneath the route policy and map to 403 with a body.
/// </summary>
public static class ComplianceErrors
{
    // ---- Actor / role ----

    public static readonly Error ActorRequired = Error.Forbidden(
        "Drivers.Hos.ActorRequired", "A signed-in user is required to record this.");

    public static readonly Error DeferralRequiresComplianceOfficer = Error.Forbidden(
        "Drivers.Hos.DeferralRequiresComplianceOfficer",
        "Only a compliance officer (or the Owner acting as one) may approve an off-duty deferral.");

    public static readonly Error AcknowledgeRequiresDispatch = Error.Forbidden(
        "Drivers.Hos.AcknowledgeRequiresDispatch",
        "Only dispatch or a compliance officer may acknowledge an exception.");

    public static readonly Error ComplianceOfficerRequired = Error.Forbidden(
        "Drivers.Hos.ComplianceOfficerRequired",
        "Only a compliance officer (or the Owner acting as one) may do this.");

    public static readonly Error ExceptionWithdrawRequiresComplianceOfficer = Error.Forbidden(
        "Drivers.Hos.ExceptionWithdrawRequiresComplianceOfficer",
        "Only a compliance officer may withdraw an exception that has already been acknowledged or approved.");

    // ---- HosException ----

    public static readonly Error ExceptionNotFound = Error.NotFound(
        "Drivers.Hos.ExceptionNotFound", "No hours-of-service exception with that id exists for this driver.");

    public static readonly Error ExceptionKindInvalid = Error.Validation(
        "Drivers.Hos.ExceptionKindInvalid",
        "A valid exception kind is required: Adverse driving conditions, Emergency, Off-duty deferral (CO approved).");

    public static readonly Error ExceptionReasonRequired = Error.Validation(
        "Drivers.Hos.ExceptionReasonRequired", "A reason is required.");

    public static readonly Error ExceptionReasonTooLong = Error.Validation(
        "Drivers.Hos.ExceptionReasonTooLong", $"The reason must be at most {HosException.ReasonMaxLength} characters.");

    public static readonly Error ExceptionBackdated = Error.Validation(
        "Drivers.Hos.ExceptionBackdated",
        "An adverse-conditions or emergency exception must be declared within 24 hours of the conditions arising.");

    public static readonly Error ExceptionInFuture = Error.Validation(
        "Drivers.Hos.ExceptionInFuture", "The exception's start is more than five minutes in the future.");

    public static readonly Error ExceptionSpanInvalid = Error.Validation(
        "Drivers.Hos.ExceptionSpanInvalid", "The exception's end must not be before its start.");

    public static readonly Error AdverseKnowabilityRequired = Error.Validation(
        "Drivers.Hos.AdverseKnowabilityRequired",
        "Adverse driving conditions can only be declared when they could not reasonably have been known before departure (s.76).");

    public static readonly Error AdverseExtensionRequired = Error.Validation(
        "Drivers.Hos.AdverseExtensionRequired",
        "An adverse-conditions exception must say how much extra driving it needs (at least one minute).");

    public static readonly Error AdverseExtensionTooLong = Error.Validation(
        "Drivers.Hos.AdverseExtensionTooLong",
        "Adverse driving conditions extend the limits by at most two hours (s.76).");

    public static readonly Error DeferralTripRequired = Error.Validation(
        "Drivers.Hos.DeferralTripRequired", "An off-duty deferral must name the trip it is approved for.");

    public static readonly Error DeferralNotInFuture = Error.Validation(
        "Drivers.Hos.DeferralNotInFuture", "An off-duty deferral must start in the future.");

    public static readonly Error DeferralTooFarAhead = Error.Validation(
        "Drivers.Hos.DeferralTooFarAhead", "An off-duty deferral cannot be approved more than seven days ahead.");

    public static readonly Error DeferralAmountInvalid = Error.Validation(
        "Drivers.Hos.DeferralAmountInvalid",
        "An off-duty deferral defers between one minute and two hours of off-duty time (s.16).");

    public static readonly Error ExceptionClosed = Error.Conflict(
        "Drivers.Hos.ExceptionClosed", "That exception has already been rejected or withdrawn.");

    // ---- DriverOutOfService ----

    public static readonly Error OutOfServiceNotFound = Error.NotFound(
        "Drivers.Hos.OutOfServiceNotFound", "No out-of-service record with that id exists for this driver.");

    public static readonly Error OutOfServiceReasonRequired = Error.Validation(
        "Drivers.Hos.OutOfServiceReasonRequired", "The reason enforcement gave is required.");

    public static readonly Error OutOfServiceDeclaredInFuture = Error.Validation(
        "Drivers.Hos.OutOfServiceDeclaredInFuture", "An out-of-service declaration cannot be dated in the future.");

    public static readonly Error OutOfServiceUntilInvalid = Error.Validation(
        "Drivers.Hos.OutOfServiceUntilInvalid", "The out-of-service period must end after it was declared.");

    public static readonly Error AlreadyOutOfService = Error.Conflict(
        "Drivers.Hos.AlreadyOutOfService",
        "This driver already has an active out-of-service declaration; lift it before recording another.");

    public static readonly Error LiftReasonRequired = Error.Validation(
        "Drivers.Hos.LiftReasonRequired", "Lifting an out-of-service declaration early requires a reason.");

    // ---- EldMalfunctionReport ----

    public static readonly Error EldMalfunctionNotFound = Error.NotFound(
        "Drivers.Hos.EldMalfunctionNotFound", "No ELD malfunction report with that id exists for this driver.");

    public static readonly Error EldCodeRequired = Error.Validation(
        "Drivers.Hos.EldCodeRequired", "The malfunction code the ELD displayed is required.");

    public static readonly Error EldCodeTooLong = Error.Validation(
        "Drivers.Hos.EldCodeTooLong", $"The malfunction code must be at most {EldMalfunctionReport.CodeMaxLength} characters.");

    public static readonly Error EldDescriptionRequired = Error.Validation(
        "Drivers.Hos.EldDescriptionRequired", "A description of the malfunction is required.");

    public static readonly Error EldNoticedInFuture = Error.Validation(
        "Drivers.Hos.EldNoticedInFuture", "The malfunction cannot have been noticed in the future.");

    // ---- ComplianceFinding ----

    public static readonly Error FindingNotFound = Error.NotFound(
        "Drivers.Hos.FindingNotFound", "No compliance finding with that id exists for this driver.");

    public static readonly Error FindingRuleInvalid = Error.Validation(
        "Drivers.Hos.FindingRuleInvalid",
        "A finding's rule must be an hours-of-service rule id (e.g. DailyDriving13h) or one of: Falsification, Tampering, TwoRecords, OutOfServiceDeclared.");

    public static readonly Error FindingDescriptionRequired = Error.Validation(
        "Drivers.Hos.FindingDescriptionRequired", "A description of the finding is required.");

    public static readonly Error FindingSeverityInvalid = Error.Validation(
        "Drivers.Hos.FindingSeverityInvalid", "A valid severity is required: Minor, Major, Severe.");

    public static readonly Error FindingSourceInvalid = Error.Validation(
        "Drivers.Hos.FindingSourceInvalid", "A valid source is required: Audit, Enforcement, Auto.");

    public static readonly Error FindingStatusInvalid = Error.Validation(
        "Drivers.Hos.FindingStatusInvalid", "A valid status is required: Open, Corrected, Closed.");

    public static readonly Error FindingClosed = Error.Conflict(
        "Drivers.Hos.FindingClosed", "That finding is closed; open a new finding for a recurrence.");

    public static readonly Error FindingTransitionInvalid = Error.Conflict(
        "Drivers.Hos.FindingTransitionInvalid", "A finding moves Open → Corrected → Closed (or Open → Closed), never backwards.");

    // ---- Prior-records attestation ----

    public static readonly Error PriorRecordsDateInFuture = Error.Validation(
        "Drivers.Hos.PriorRecordsDateInFuture", "Prior records can only be attested through today or earlier.");

    public static readonly Error PriorRecordsNoteTooLong = Error.Validation(
        "Drivers.Hos.PriorRecordsNoteTooLong", "The attestation note must be at most 1000 characters.");
}
