using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Application.Hos;

/// <summary>
/// The single enum ⇄ friendly-string boundary for HOS. The database stores the clean
/// <see cref="DutyStatus"/> / <see cref="HosLogEntrySource"/> enums; the wire carries the
/// display strings the Dispatch Console already keys on (its <c>dutyMeta</c>, <c>DutyChip</c>,
/// and <c>SourceChip</c> compare these exact strings), so emitting them keeps the frontend a
/// pure data-source swap.
/// </summary>
public static class HosDisplay
{
    public const string OffDuty = "Off Duty";
    public const string OnDuty = "On Duty";
    public const string Driving = "Driving";

    public const string DriverAppSource = "Driver App";
    public const string ManualPaperBackupSource = "Manual (paper backup)";

    public static string DutyToWire(DutyStatus duty) => duty switch
    {
        DutyStatus.OffDuty => OffDuty,
        DutyStatus.OnDuty => OnDuty,
        DutyStatus.Driving => Driving,
        _ => duty.ToString(),
    };

    /// <summary>Parses the friendly duty string sent by the POST body; invalid input is a domain error.</summary>
    public static Result<DutyStatus> DutyFromWire(string? value) => value?.Trim() switch
    {
        OffDuty => Result.Success(DutyStatus.OffDuty),
        OnDuty => Result.Success(DutyStatus.OnDuty),
        Driving => Result.Success(DutyStatus.Driving),
        _ => Result.Failure<DutyStatus>(HosErrors.InvalidDuty),
    };

    public static string SourceToWire(HosLogEntrySource source) => source switch
    {
        HosLogEntrySource.DriverApp => DriverAppSource,
        HosLogEntrySource.ManualPaperBackup => ManualPaperBackupSource,
        _ => source.ToString(),
    };

    /// <summary>
    /// Parses the friendly source string sent by the POST body — the exact inverse of
    /// <see cref="SourceToWire"/>, accepting only the two strings it emits. Keeping the pair
    /// symmetric makes the Dispatch Console's existing chip strings the contract, so there is one
    /// vocabulary for HOS sources rather than a display one and a wire one that can drift.
    /// <para>
    /// <b>Absent means Manual (paper backup)</b>, preserving the behaviour from when the endpoint
    /// hardcoded it: the Dispatch Console sends no source field, and defaulting the other way
    /// would relabel every dispatcher entry as a driver submission and corrupt the HOS record.
    /// </para>
    /// <para>An unrecognized string is a validation error, never an exception — an offline Field
    /// App replaying a queue must get a 400 it can park, not a 500.</para>
    /// </summary>
    public static Result<HosLogEntrySource> SourceFromWire(string? value) => value?.Trim() switch
    {
        null or "" => Result.Success(HosLogEntrySource.ManualPaperBackup),
        DriverAppSource => Result.Success(HosLogEntrySource.DriverApp),
        ManualPaperBackupSource => Result.Success(HosLogEntrySource.ManualPaperBackup),
        _ => Result.Failure<HosLogEntrySource>(HosErrors.InvalidSource),
    };
}
