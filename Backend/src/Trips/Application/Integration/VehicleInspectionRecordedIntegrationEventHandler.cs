using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Enforces the completion gate from Fleet's inspection stream: a trip cannot Complete until
/// a post-trip inspection has been logged for it. On a <c>PostTrip</c> inspection carrying a
/// trip number, the matching trip's <c>HasPostTripInspection</c> flag is flipped so
/// <c>Trip.Complete()</c> stops refusing. Pre-trip inspections and inspections whose trip
/// number matches no trip (ad-hoc / standalone vehicle entries) are ignored. Handlers run
/// outside any HTTP request, so the event's tenant is pushed as the ambient tenant for the
/// write (RLS session variable). Delivery is at-least-once and the flag flip is idempotent, so
/// replays converge. Mirrors <see cref="VehicleChangedIntegrationEventHandler"/>.
/// </summary>
public sealed class VehicleInspectionRecordedIntegrationEventHandler(
    ITripRepository repository,
    ILogger<VehicleInspectionRecordedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<VehicleInspectionRecordedIntegrationEvent>
{
    private const string PostTrip = "PostTrip";

    public async Task Handle(VehicleInspectionRecordedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.InspectionType != PostTrip || string.IsNullOrWhiteSpace(integrationEvent.TripNumber))
        {
            // Pre-trip inspection, or a standalone entry with no trip context — nothing to gate.
            return;
        }

        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            var trip = await repository.GetByTripNumberAsync(integrationEvent.TripNumber, cancellationToken);
            if (trip is null)
            {
                // Ad-hoc inspection with no planned trip under this tenant — perfectly normal.
                logger.LogInformation(
                    "Trips saw post-trip inspection {InspectionId} for trip {TripNumber} of tenant {TenantId} ({EventId}) with no matching trip; ignoring.",
                    integrationEvent.InspectionId, integrationEvent.TripNumber, integrationEvent.TenantId, integrationEvent.EventId);
                return;
            }

            trip.RecordPostTripInspection();
            await repository.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Trips recorded post-trip inspection {InspectionId} against trip {TripNumber} of tenant {TenantId} ({EventId}); completion gate cleared.",
            integrationEvent.InspectionId, integrationEvent.TripNumber, integrationEvent.TenantId, integrationEvent.EventId);
    }
}
