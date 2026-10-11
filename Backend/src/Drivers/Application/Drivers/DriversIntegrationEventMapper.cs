using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Application.Drivers;

/// <summary>
/// Drivers' explicit domain-event → integration-event translation. Register, update, status
/// change, and linking/unlinking the Identity user all map to the single upsert-shaped
/// <c>drivers.driver-changed</c> event — consumers (Trips' <c>driver_lookup</c>) maintain a
/// replica keyed on DriverId, so one event carrying current state covers all of them. The
/// link events are on this list because Trips resolves a Driver Field App caller to "their"
/// driver through the replicated <c>UserId</c>: miss them and an unlink would leave a revoked
/// account still able to act on that driver's trips until the next unrelated edit. The three
/// <see cref="DutyDay"/> events map the same way to <c>drivers.duty-day-changed</c>, carrying
/// the day's whole effective timeline. Credentials, clearances and the legacy totals stay
/// internal (null). Extending Drivers' public surface means adding a case here plus an event
/// record in NorthernLink.Shared/IntegrationEvents/Drivers/ — never auto-publishing.
/// </summary>
public sealed class DriversIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            DriverRegisteredDomainEvent or DriverUpdatedDomainEvent or DriverStatusChangedDomainEvent
                or DriverUserLinkedDomainEvent or DriverUserUnlinkedDomainEvent =>
                ToDriverChanged((Driver)aggregate),
            DutyEventAppendedDomainEvent or DutyEventAmendedDomainEvent or DutyDayCertifiedDomainEvent =>
                ToDutyDayChanged((DutyDay)aggregate),
            _ => null,
        };

    private static DriverChangedIntegrationEvent ToDriverChanged(Driver driver) => new(
        driver.Id,
        driver.TenantId,
        driver.Name,
        driver.LicenceClass,
        driver.Status.ToString(),
        driver.Source,
        driver.UserId);

    private static DutyDayChangedIntegrationEvent ToDutyDayChanged(DutyDay day) => new(
        day.Id,
        day.TenantId,
        day.DriverId,
        day.LocalDate,
        day.EffectiveTimeline().Select(ToSnapshot).ToList(),
        day.IsCertified,
        day.PersonalConveyanceKm,
        day.Version);

    private static DutyEventSnapshot ToSnapshot(DutyEvent e) => new(
        e.EventId,
        e.AtUtc,
        HosDisplay.DutyToWire(e.Status),
        HosDisplay.SourceToWire(e.Source),
        e.CarrierName,
        e.OdometerStartKm,
        e.OdometerEndKm);
}
