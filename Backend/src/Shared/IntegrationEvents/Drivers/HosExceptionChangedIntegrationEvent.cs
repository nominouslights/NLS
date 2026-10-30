using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Drivers;

/// <summary>
/// Published whenever an hours-of-service exception is declared, acknowledged, approved,
/// rejected or withdrawn — routing key <c>drivers.hos-exception-changed</c>. Upsert-shaped on
/// <see cref="ExceptionId"/>: it carries the current <see cref="Status"/>, and a consumer stops
/// honouring the exception the moment it reads <c>Rejected</c> or <c>Withdrawn</c>.
/// <see cref="Kind"/> is the <c>HosWire</c> kind string ("Adverse driving conditions",
/// "Emergency", "Off-duty deferral (CO approved)") and <see cref="Status"/> the status member
/// name — never Drivers' internal enums. <see cref="ExtraDrivingMinutes"/> is the adverse
/// extension (null for the other kinds); <see cref="DeferredOffDutyMinutes"/> the deferral
/// amount (null for the other kinds). Nobody consumes it yet; Trips' hours gate replica
/// (<c>driver_hos_exceptions</c>) is the planned consumer.
/// </summary>
public sealed record HosExceptionChangedIntegrationEvent(
    Guid ExceptionId,
    Guid TenantId,
    Guid DriverId,
    string Kind,
    string Status,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    Guid? TripId,
    int? ExtraDrivingMinutes,
    int? DeferredOffDutyMinutes = null) : IntegrationEvent;
