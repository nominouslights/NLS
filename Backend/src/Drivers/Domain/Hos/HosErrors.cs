using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// All domain errors the HOS aggregates (<see cref="HosLogEntry"/>, <see cref="DutyDay"/>) and
/// their handlers can produce. The "valid values" lists in the messages are the
/// <c>HosWire</c> strings, so a 400 tells the caller exactly what to send.
/// </summary>
public static class HosErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Drivers.Hos.NotFound", "The HOS log entry was not found.");

    public static readonly Error EnteredByRequired = Error.Validation(
        "Drivers.Hos.EnteredByRequired",
        "A dispatcher name (enteredBy) is required for a transcribed entry (ELD transcription or Manual (paper backup)).");

    public static readonly Error HoursOutOfRange = Error.Validation(
        "Drivers.Hos.HoursOutOfRange", "Each hours value must be between 0 and 24.");

    public static readonly Error InvalidDuty = Error.Validation(
        "Drivers.Hos.InvalidDuty",
        "A valid duty status is required: Off Duty, On Duty, Driving, Personal Conveyance.");

    public static readonly Error InvalidSource = Error.Validation(
        "Drivers.Hos.InvalidSource",
        "A valid source is required: Driver App, ELD transcription, Manual (paper backup), Other carrier (declared); omit it for a paper-backup entry.");

    /// <summary>The legacy daily-totals route only knows two sources.</summary>
    public static readonly Error LegacySourceUnsupported = Error.Validation(
        "Drivers.Hos.InvalidSource",
        "The daily-totals entry accepts only Driver App or Manual (paper backup); record other sources as duty events.");

    public static readonly Error SystemSourceReserved = Error.Validation(
        "Drivers.Hos.SystemSourceReserved",
        "The System source is minted by the ledger itself and cannot be submitted.");

    public static readonly Error CarrierNameRequired = Error.Validation(
        "Drivers.Hos.CarrierNameRequired",
        "A carrier name is required for work declared for another carrier.");

    // ---- DutyDay ----

    public static readonly Error DayNotFound = Error.NotFound(
        "Drivers.Hos.DayNotFound", "No duty record exists for that driver on that day.");

    public static readonly Error DayInFuture = Error.Validation(
        "Drivers.Hos.DayInFuture", "A day cannot be certified before it starts.");

    public static readonly Error EventOutsideDay = Error.Validation(
        "Drivers.Hos.EventOutsideDay",
        "The event's instant falls outside the duty day it was recorded against (America/Winnipeg local date).");

    public static readonly Error EventInFuture = Error.Validation(
        "Drivers.Hos.EventInFuture", "The event's instant is more than five minutes in the future.");

    public static readonly Error NoStatusChange = Error.Validation(
        "Drivers.Hos.NoStatusChange",
        "The driver is already in that duty status at that instant; a duty event must change the status.");

    public static readonly Error PcOdometerRequired = Error.Validation(
        "Drivers.Hos.PcOdometerRequired",
        "Personal conveyance must start with the vehicle's odometer reading (odometerStartKm).");

    public static readonly Error PcAttestationRequired = Error.Validation(
        "Drivers.Hos.PcAttestationRequired",
        "Personal conveyance requires all three attestations: no passengers or cargo, no trailer, and no work being performed.");

    public static readonly Error PcAllowanceExhausted = Error.Validation(
        "Drivers.Hos.PcAllowanceExhausted",
        "The 75.0 km personal-conveyance allowance for this day is used up; further driving is work.");

    public static readonly Error PcOdometerEndRequired = Error.Validation(
        "Drivers.Hos.PcOdometerEndRequired",
        "The event ending personal conveyance must carry the vehicle's odometer reading (odometerEndKm).");

    public static readonly Error PcOdometerDecreasing = Error.Validation(
        "Drivers.Hos.PcOdometerDecreasing",
        "The odometer reading ending personal conveyance is lower than the reading that started it.");

    public static readonly Error OdometerNegative = Error.Validation(
        "Drivers.Hos.OdometerNegative", "An odometer reading cannot be negative.");

    public static readonly Error PcSegmentOpen = Error.Validation(
        "Drivers.Hos.PcSegmentOpen",
        "The day ends in personal conveyance with no closing odometer; record the end of the segment before certifying.");

    public static readonly Error NothingToCertify = Error.Validation(
        "Drivers.Hos.NothingToCertify", "The day has no effective duty events to certify.");

    public static readonly Error CertifyDriverOnly = Error.Validation(
        "Drivers.Hos.CertifyDriverOnly",
        "Only the driver may certify their own record of duty status; dispatch cannot certify on a driver's behalf.");

    public static readonly Error EventNotFound = Error.NotFound(
        "Drivers.Hos.EventNotFound",
        "No duty event with that id exists on the day the replacement falls in.");

    public static readonly Error EventAlreadyVoided = Error.Conflict(
        "Drivers.Hos.EventAlreadyVoided", "That duty event has already been amended.");

    public static readonly Error AmendmentReasonRequired = Error.Validation(
        "Drivers.Hos.AmendmentReasonRequired", "An amendment must carry a reason.");

    public static readonly Error TranscriptionEmpty = Error.Validation(
        "Drivers.Hos.TranscriptionEmpty", "A transcription must contain at least one entry.");

    public static readonly Error TranscriptionNotChronological = Error.Validation(
        "Drivers.Hos.TranscriptionNotChronological",
        "Transcription entries must be in strictly increasing time order.");

    public static readonly Error TranscriptionSourceInvalid = Error.Validation(
        "Drivers.Hos.TranscriptionSourceInvalid",
        "A transcription's source must be ELD transcription or Manual (paper backup).");

    public static readonly Error InvalidTime = Error.Validation(
        "Drivers.Hos.InvalidTime", "Times must be local HH:mm (24-hour).");
}
