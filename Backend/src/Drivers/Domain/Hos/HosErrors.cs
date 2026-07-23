using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>All domain errors the HosLogEntry aggregate (and its handlers) can produce.</summary>
public static class HosErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Drivers.Hos.NotFound", "The HOS log entry was not found.");

    public static readonly Error EnteredByRequired = Error.Validation(
        "Drivers.Hos.EnteredByRequired", "A dispatcher name (enteredBy) is required for a manual paper-backup entry.");

    public static readonly Error HoursOutOfRange = Error.Validation(
        "Drivers.Hos.HoursOutOfRange", "Each hours value must be between 0 and 24.");

    public static readonly Error InvalidDuty = Error.Validation(
        "Drivers.Hos.InvalidDuty", "A valid duty status (Off Duty, On Duty, Driving) is required.");
}
