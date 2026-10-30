using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyEvents.Record;

/// <summary>
/// Appends one duty change to the driver's ledger. The day is derived from
/// <paramref name="AtUtc"/> (America/Winnipeg) and opened if it does not exist yet.
/// <paramref name="EventId"/> is the client's idempotency key (the Field App mints it offline,
/// or the <c>Idempotency-Key</c> header stands in); null means the server mints one, which
/// makes the request non-replayable. The recording actor comes from the token, never the body.
/// </summary>
public sealed record RecordDutyEventCommand(
    Guid TenantId,
    Guid DriverId,
    Guid? EventId,
    DateTimeOffset AtUtc,
    DutyStatus Status,
    DutyEventSource Source,
    string? EnteredBy,
    string? CarrierName,
    string? Note,
    decimal? OdometerStartKm,
    decimal? OdometerEndKm,
    bool PcNoPassengersOrCargo,
    bool PcNoTrailer,
    bool PcNoWork) : ICommand<DutyEventRecordedResponse>;
