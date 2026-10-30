using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Drivers;

/// <summary>
/// Published whenever a driver's duty day changes — an event appended, an event amended, or the
/// day certified — routing key <c>drivers.duty-day-changed</c>. Upsert-shaped: it carries the
/// day's whole <b>effective</b> timeline (voided events already removed) plus the certification
/// and personal-conveyance figures, so a consumer maintains a replica keyed on
/// <see cref="DutyDayId"/> by replacing the row when <see cref="Version"/> is newer and ignoring
/// it otherwise (at-least-once delivery, out-of-order safe). Status and source travel as the
/// <c>HosWire</c> strings, never Drivers' internal enums. Nobody consumes it yet; Trips' hours
/// gate replica (<c>driver_duty_days</c>) is the planned consumer.
/// </summary>
public sealed record DutyDayChangedIntegrationEvent(
    Guid DutyDayId,
    Guid TenantId,
    Guid DriverId,
    DateOnly LocalDate,
    IReadOnlyList<DutyEventSnapshot> EffectiveEvents,
    bool Certified,
    decimal PersonalConveyanceKm,
    int Version) : IntegrationEvent;

/// <summary>
/// One effective duty-status change inside <see cref="DutyDayChangedIntegrationEvent"/>.
/// <paramref name="Status"/> and <paramref name="Source"/> are <c>HosWire</c> strings
/// ("Driving", "ELD transcription", …). Odometers are kilometres to one decimal; the opening
/// event of a personal-conveyance segment carries the start reading, the event closing it the
/// end reading — exactly what the rule engine's <c>OdometerDecikm</c> needs after ×10.
/// </summary>
public sealed record DutyEventSnapshot(
    Guid EventId,
    DateTimeOffset AtUtc,
    string Status,
    string Source,
    string? CarrierName,
    decimal? OdometerStartKm,
    decimal? OdometerEndKm);
