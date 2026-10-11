using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using DutyStatus = NorthernLink.Drivers.Domain.Hos.DutyStatus;
using EngineStatus = NorthernLink.Shared.HoursOfService.DutyStatus;

namespace NorthernLink.Drivers.Application.Hos;

/// <summary>
/// The single enum ⇄ wire-string boundary for HOS in the Drivers module. The database stores
/// the clean Drivers enums (<see cref="DutyStatus"/>, <see cref="DutyEventSource"/>,
/// <see cref="HosLogEntrySource"/>); the wire carries the strings the Dispatch Console and the
/// Driver Field App key on — and since the rule engine landed, every string comes from
/// <see cref="HosWire"/>, so the two frontends, the engine's TypeScript mirror and this module
/// share one vocabulary. The Drivers enums are spelled member-for-member like the engine's, so
/// the mapping between them is by name and can never drift silently.
/// </summary>
public static class HosDisplay
{
    public const string OffDuty = HosWire.OffDuty;
    public const string OnDuty = HosWire.OnDuty;
    public const string Driving = HosWire.Driving;
    public const string PersonalConveyance = HosWire.PersonalConveyance;

    public const string DriverAppSource = HosWire.DriverAppSource;
    public const string EldTranscriptionSource = HosWire.EldTranscriptionSource;
    public const string ManualPaperBackupSource = HosWire.PaperLogSource;
    public const string OtherCarrierSource = HosWire.OtherCarrierSource;
    public const string SystemSource = HosWire.SystemSource;

    /// <summary>
    /// The sentence every HOS figure the API shows carries. The record of duty status lives on
    /// the bus's ELD; this ledger plans against it and mirrors it, and must never be mistaken
    /// for it (keeping two records is itself a violation).
    /// </summary>
    public const string PlanningLabel =
        "Planning figures from the app ledger — not the record of duty status (the ELD is the RODS)";

    // ---- Duty status ----

    public static string DutyToWire(DutyStatus duty) => HosWire.Status(ToEngine(duty));

    /// <summary>Parses the wire duty string; an unrecognised value is a validation error listing the valid ones.</summary>
    public static Result<DutyStatus> DutyFromWire(string? value) =>
        HosWire.StatusFrom(value) is { } status
            ? Result.Success(FromEngine(status))
            : Result.Failure<DutyStatus>(HosErrors.InvalidDuty);

    public static EngineStatus ToEngine(DutyStatus duty) => duty switch
    {
        DutyStatus.OffDuty => EngineStatus.OffDuty,
        DutyStatus.OnDuty => EngineStatus.OnDuty,
        DutyStatus.Driving => EngineStatus.Driving,
        DutyStatus.PersonalConveyance => EngineStatus.PersonalConveyance,
        _ => throw new ArgumentOutOfRangeException(nameof(duty), duty, "Unknown duty status."),
    };

    public static DutyStatus FromEngine(EngineStatus status) => status switch
    {
        EngineStatus.OffDuty => DutyStatus.OffDuty,
        EngineStatus.OnDuty => DutyStatus.OnDuty,
        EngineStatus.Driving => DutyStatus.Driving,
        EngineStatus.PersonalConveyance => DutyStatus.PersonalConveyance,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown duty status."),
    };

    // ---- Duty-event source (the ledger) ----

    public static string SourceToWire(DutyEventSource source) => HosWire.Source(ToEngine(source));

    /// <summary>
    /// Parses the wire source string for a duty event.
    /// <para>
    /// <b>Absent means Manual (paper backup)</b>, preserving the behaviour from when the
    /// endpoint hardcoded it: the Dispatch Console sends no source field, and defaulting the
    /// other way would relabel every dispatcher entry as a driver submission and corrupt the
    /// record.
    /// </para>
    /// <para>An unrecognized string is a validation error, never an exception — an offline Field
    /// App replaying a queue must get a 400 it can park, not a 500.</para>
    /// </summary>
    public static Result<DutyEventSource> SourceFromWire(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success(DutyEventSource.PaperLog);
        }

        return HosWire.SourceFrom(value) is { } source
            ? Result.Success(FromEngine(source))
            : Result.Failure<DutyEventSource>(HosErrors.InvalidSource);
    }

    public static EventSource ToEngine(DutyEventSource source) => source switch
    {
        DutyEventSource.DriverApp => EventSource.DriverApp,
        DutyEventSource.EldTranscription => EventSource.EldTranscription,
        DutyEventSource.PaperLog => EventSource.PaperLog,
        DutyEventSource.OtherCarrier => EventSource.OtherCarrier,
        DutyEventSource.System => EventSource.System,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown event source."),
    };

    public static DutyEventSource FromEngine(EventSource source) => source switch
    {
        EventSource.DriverApp => DutyEventSource.DriverApp,
        EventSource.EldTranscription => DutyEventSource.EldTranscription,
        EventSource.PaperLog => DutyEventSource.PaperLog,
        EventSource.OtherCarrier => DutyEventSource.OtherCarrier,
        EventSource.System => DutyEventSource.System,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown event source."),
    };

    // ---- Legacy daily-totals source ----

    public static string SourceToWire(HosLogEntrySource source) => source switch
    {
        HosLogEntrySource.DriverApp => DriverAppSource,
        HosLogEntrySource.ManualPaperBackup => ManualPaperBackupSource,
        _ => source.ToString(),
    };

    /// <summary>
    /// The legacy totals path knows two sources. Absent still means Manual (paper backup); the
    /// newer strings parse (so the error is "not on this route" rather than "unknown"), but only
    /// Driver App and Manual (paper backup) are accepted here.
    /// </summary>
    public static Result<HosLogEntrySource> LegacySourceFromWire(string? value)
    {
        var parsed = SourceFromWire(value);
        if (parsed.IsFailure)
        {
            return Result.Failure<HosLogEntrySource>(parsed.Error);
        }

        return parsed.Value switch
        {
            DutyEventSource.DriverApp => Result.Success(HosLogEntrySource.DriverApp),
            DutyEventSource.PaperLog => Result.Success(HosLogEntrySource.ManualPaperBackup),
            _ => Result.Failure<HosLogEntrySource>(HosErrors.LegacySourceUnsupported),
        };
    }
}
