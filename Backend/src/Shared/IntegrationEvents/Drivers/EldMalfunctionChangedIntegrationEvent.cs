using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Drivers;

/// <summary>
/// Published when an ELD malfunction is reported or resolved — routing key
/// <c>drivers.eld-malfunction-changed</c>. Upsert-shaped on <see cref="ReportId"/>;
/// <see cref="Status"/> is "Open" or "Resolved". Carries the driver's display name so the
/// planned Notifications consumer can address the compliance alert email without a lookup.
/// Nobody consumes it yet.
/// </summary>
public sealed record EldMalfunctionChangedIntegrationEvent(
    Guid ReportId,
    Guid TenantId,
    Guid DriverId,
    string DriverName,
    string Code,
    string Status,
    DateTimeOffset NoticedAtUtc,
    string Description) : IntegrationEvent;
