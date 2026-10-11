using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyEvents.Amend;

/// <summary>
/// Voids <paramref name="OriginalEventId"/> and appends the replacement with
/// <paramref name="Reason"/>. Amendments are day-scoped, so the day is the one the
/// replacement's <paramref name="AtUtc"/> falls in; an original not on that day is "not found".
/// <paramref name="ReplacementEventId"/> is the idempotency key (null → server-minted).
/// </summary>
public sealed record AmendDutyEventCommand(
    Guid TenantId,
    Guid DriverId,
    Guid OriginalEventId,
    Guid? ReplacementEventId,
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
    bool PcNoWork,
    string? Reason) : ICommand<DutyEventAmendedResponse>;
