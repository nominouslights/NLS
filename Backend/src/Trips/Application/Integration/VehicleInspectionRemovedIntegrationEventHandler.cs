using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// The inverse of <see cref="VehicleInspectionRecordedIntegrationEventHandler"/>: re-arms the
/// completion gate when a post-trip inspection is deleted in Fleet. On a <c>PostTrip</c> removal
/// carrying a trip number, the matching trip's <c>HasPostTripInspection</c> flag is cleared so
/// <c>Trip.Complete()</c> refuses again until a fresh post-trip DVIR is logged. Pre-trip removals
/// and removals whose trip number matches no trip (ad-hoc / standalone vehicle entries) are
/// ignored. Handlers run outside any HTTP request, so the event's tenant is pushed as the ambient
/// tenant for the write (RLS session variable). Delivery is at-least-once and the flag clear is
/// idempotent, so replays converge.
/// </summary>
public sealed class VehicleInspectionRemovedIntegrationEventHandler(
    ITripRepository repository,
    ILogger<VehicleInspectionRemovedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<VehicleInspectionRemovedIntegrationEvent>
{
    private const string PostTrip = "PostTrip";

    public async Task Handle(VehicleInspectionRemovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.InspectionType != PostTrip || string.IsNullOrWhiteSpace(integrationEvent.TripNumber))
        {
            // Pre-trip inspection, or a standalone entry with no trip context — nothing to re-gate.
            return;
        }

        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            var trip = await repository.GetByTripNumberAsync(integrationEvent.TripNumber, cancellationToken);
            if (trip is null)
            {
                // Ad-hoc inspection with no planned trip under this tenant — perfectly normal.
                logger.LogInformation(
                    "Trips saw post-trip inspection {InspectionId} removed for trip {TripNumber} of tenant {TenantId} ({EventId}) with no matching trip; ignoring.",
                    integrationEvent.InspectionId, integrationEvent.TripNumber, integrationEvent.TenantId, integrationEvent.EventId);
                return;
            }

            trip.ClearPostTripInspection();
            await repository.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Trips cleared post-trip inspection {InspectionId} from trip {TripNumber} of tenant {TenantId} ({EventId}); completion gate re-armed.",
            integrationEvent.InspectionId, integrationEvent.TripNumber, integrationEvent.TenantId, integrationEvent.EventId);
    }
}
