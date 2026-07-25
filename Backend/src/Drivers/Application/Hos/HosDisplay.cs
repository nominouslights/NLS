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
}
