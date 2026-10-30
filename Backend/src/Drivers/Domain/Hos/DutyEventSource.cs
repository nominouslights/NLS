namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// Where a <see cref="DutyEvent"/> came from. Member spellings match the rule engine's
/// <c>NorthernLink.Shared.HoursOfService.EventSource</c>; the wire strings are
/// <c>HosWire</c>'s ("Driver App", "ELD transcription", "Manual (paper backup)",
/// "Other carrier (declared)", "System"). <see cref="PaperLog"/> is the ledger's name for what
/// the legacy totals path (<c>HosLogEntrySource.ManualPaperBackup</c>) still calls a paper
/// backup — same wire string, same meaning: a dispatcher keyed it from paper.
/// </summary>
public enum DutyEventSource
{
    /// <summary>A timestamped duty change the driver made on the tablet.</summary>
    DriverApp,

    /// <summary>Dispatch keyed it from the bus's ELD display or printout; <c>EnteredBy</c> is required.</summary>
    EldTranscription,

    /// <summary>Dispatch keyed it from a paper log; <c>EnteredBy</c> is required.</summary>
    PaperLog,

    /// <summary>Work the driver declared for another carrier; <c>CarrierName</c> is required.</summary>
    OtherCarrier,

    /// <summary>
    /// Minted by the aggregate itself — today only the automatic switch to on-duty at the
    /// 75.0 km personal-conveyance limit. Never accepted from a client.
    /// </summary>
    System,
}
