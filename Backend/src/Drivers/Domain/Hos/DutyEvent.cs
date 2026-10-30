namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// One duty-status change inside a <see cref="DutyDay"/>, persisted as an element of the day's
/// append-only jsonb <c>events</c> array. Nothing here is ever edited in place: a correction is
/// a new event with <see cref="AmendsEventId"/> pointing at the original, which is marked
/// <see cref="IsVoided"/> and kept — the audit trail is the whole point.
///
/// <para>
/// <see cref="EventId"/> is the idempotency key: the Driver Field App mints it offline, so a
/// replayed queue item is recognised and ignored rather than duplicated. <see cref="Sequence"/>
/// is the day-local append order and the engine's tie-break for events at the same instant
/// (highest wins). <see cref="RecordedByUserId"/> is the authenticated actor, never a body
/// field; <see cref="EnteredBy"/> is the typed name a transcription carries, exactly as
/// <c>HosLogEntry.EnteredBy</c> did.
/// </para>
///
/// <para>
/// Odometers are kilometres to one decimal (the engine takes tenths of a kilometre). The event
/// that opens a <see cref="DutyStatus.PersonalConveyance"/> segment carries
/// <see cref="OdometerStartKm"/> plus the three attestations; the event that closes it carries
/// <see cref="OdometerEndKm"/>.
/// </para>
/// </summary>
public sealed record DutyEvent
{
    public required Guid EventId { get; init; }
    public required int Sequence { get; init; }
    public required DateTimeOffset AtUtc { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
    public required DutyStatus Status { get; init; }
    public required DutyEventSource Source { get; init; }
    public Guid? RecordedByUserId { get; init; }
    public string? EnteredBy { get; init; }
    public string? CarrierName { get; init; }
    public string? Note { get; init; }
    public decimal? OdometerStartKm { get; init; }
    public decimal? OdometerEndKm { get; init; }
    public bool PcNoPassengersOrCargo { get; init; }
    public bool PcNoTrailer { get; init; }
    public bool PcNoWork { get; init; }
    public Guid? AmendsEventId { get; init; }
    public string? AmendmentReason { get; init; }
    public bool IsVoided { get; init; }
}

/// <summary>
/// What a caller knows about a duty change before the aggregate accepts it — everything on
/// <see cref="DutyEvent"/> except what the aggregate assigns (<c>Sequence</c>,
/// <c>RecordedAtUtc</c>, amendment links, the voided flag). <see cref="EventId"/> is the
/// client-minted id, or a server-minted one when the client sent none.
/// </summary>
public sealed record DutyEventDraft(
    Guid EventId,
    DateTimeOffset AtUtc,
    DutyStatus Status,
    DutyEventSource Source,
    Guid? RecordedByUserId,
    string? EnteredBy = null,
    string? CarrierName = null,
    string? Note = null,
    decimal? OdometerStartKm = null,
    decimal? OdometerEndKm = null,
    bool PcNoPassengersOrCargo = false,
    bool PcNoTrailer = false,
    bool PcNoWork = false);

/// <summary>Outcome of <see cref="DutyDay.Append"/> / <see cref="DutyDay.Amend"/>.</summary>
public enum DutyAppendOutcome
{
    /// <summary>The event was added and a domain event raised.</summary>
    Recorded,

    /// <summary>
    /// An event with this id already existed. Nothing changed and no domain event was raised,
    /// so a save is a no-op rather than a trip over the eventless-write guard.
    /// </summary>
    AlreadyRecorded,
}
