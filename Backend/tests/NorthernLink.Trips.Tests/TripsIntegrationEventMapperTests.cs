using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Manifests.Events;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class TripsIntegrationEventMapperTests
{
    private readonly TripsIntegrationEventMapper _mapper = new();

    [Fact]
    public void Manifest_events_are_internal_and_do_not_publish()
    {
        // Since inspections detached from the manifest (Phase B), the manifest carries
        // nothing another module consumes — its events stay internal (map to null).
        var manifest = TestManifests.Create().Value;
        var created = (TripManifestCreatedDomainEvent)manifest.DomainEvents.Single();

        Assert.Null(_mapper.Map(created, manifest));
    }

    [Fact]
    public void Ready_for_billing_maps_to_public_integration_event_with_the_full_billing_payload()
    {
        var clientId = Guid.NewGuid();
        var trip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4821",
            clientId: clientId,
            scheduleTemplateId: Guid.NewGuid(),
            roundTripKey: "abc123:20260721",
            direction: TripDirection.Outbound).Value;
        trip.RecordPostTripInspection();
        trip.ClearDomainEvents();
        trip.FinishOperations();
        var domainEvent = (TripReadyForBillingDomainEvent)trip.DomainEvents.Single();

        var result = _mapper.Map(domainEvent, trip);

        var integrationEvent = Assert.IsType<TripReadyForBillingIntegrationEvent>(result);
        Assert.Equal(trip.Id, integrationEvent.TripId);
        Assert.Equal(TestPlanning.TenantId, integrationEvent.TenantId);
        Assert.Equal("TR-4821", integrationEvent.TripNumber);
        Assert.Equal(clientId, integrationEvent.ClientId);
        Assert.Equal("Alamos Gold", integrationEvent.ClientName);
        Assert.Equal("ContractCrew", integrationEvent.ServiceType);
        Assert.Equal("Thompson ↔ Lynn Lake", integrationEvent.RouteName);
        Assert.Equal("Thompson", integrationEvent.Origin);
        Assert.Equal("Lynn Lake", integrationEvent.Destination);
        Assert.Equal(320, integrationEvent.DistanceKm);
        Assert.Equal(new DateOnly(2026, 7, 21), integrationEvent.ServiceDate);
        Assert.Equal("abc123:20260721", integrationEvent.RoundTripKey);
        Assert.Equal("Outbound", integrationEvent.Direction);
        Assert.False(integrationEvent.IsEmptyLeg);
        Assert.Equal("PO-2026-118", integrationEvent.PoNumber);
        Assert.Equal(trip.OperationsFinishedAtUtc, integrationEvent.OperationsFinishedAtUtc);
    }

    [Fact]
    public void Clientless_completion_publishes_nothing()
    {
        // A community/walk-up run never enters the billing arc: FinishOperations lands it in
        // Completed, and TripCompletedDomainEvent is internal — no billable trip is recorded.
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-9005").Value; // clientId null
        trip.RecordPostTripInspection();
        trip.ClearDomainEvents();
        trip.FinishOperations();
        var domainEvent = (TripCompletedDomainEvent)trip.DomainEvents.Single();

        Assert.Null(_mapper.Map(domainEvent, trip));
    }

    [Fact]
    public void Close_without_billing_maps_to_the_public_closed_event_with_the_reason()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-9006", clientId: Guid.NewGuid()).Value;
        trip.RecordPostTripInspection();
        trip.FinishOperations();
        trip.ClearDomainEvents();
        trip.CloseWithoutBilling("Client has no active contract");
        var domainEvent = (TripClosedWithoutBillingDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripClosedWithoutBillingIntegrationEvent>(_mapper.Map(domainEvent, trip));
        Assert.Equal(trip.Id, integrationEvent.TripId);
        Assert.Equal(TestPlanning.TenantId, integrationEvent.TenantId);
        Assert.Equal("TR-9006", integrationEvent.TripNumber);
        Assert.Equal("Client has no active contract", integrationEvent.Reason);
    }

    [Fact]
    public void Reopen_maps_to_the_public_reopened_event_with_reason_and_both_statuses()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-9007", clientId: Guid.NewGuid()).Value;
        trip.RecordPostTripInspection();
        trip.FinishOperations();
        trip.ClearDomainEvents();
        trip.Reopen("Finished before the return leg ran");
        var domainEvent = (TripReopenedDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripReopenedIntegrationEvent>(_mapper.Map(domainEvent, trip));
        Assert.Equal(trip.Id, integrationEvent.TripId);
        Assert.Equal(TestPlanning.TenantId, integrationEvent.TenantId);
        Assert.Equal("TR-9007", integrationEvent.TripNumber);
        Assert.Equal("Finished before the return leg ran", integrationEvent.Reason);
        Assert.Equal("ReadyForBilling", integrationEvent.PreviousStatus);
        Assert.Equal("Scheduled", integrationEvent.NewStatus);
    }

    [Fact]
    public void Deadhead_leg_finish_carries_the_empty_leg_flag()
    {
        var trip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-9002", clientId: Guid.NewGuid(), isEmptyLeg: true).Value;
        trip.RecordPostTripInspection();
        trip.ClearDomainEvents();
        trip.FinishOperations();
        var domainEvent = (TripReadyForBillingDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripReadyForBillingIntegrationEvent>(_mapper.Map(domainEvent, trip));
        Assert.True(integrationEvent.IsEmptyLeg);
    }

    [Fact]
    public void Round_trip_pairing_maps_to_the_public_round_trip_changed_event()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-9003", clientId: Guid.NewGuid()).Value;
        trip.ClearDomainEvents();
        trip.AssignRoundTrip("merge:abc", TripDirection.Outbound);
        var domainEvent = (TripRoundTripChangedDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripRoundTripChangedIntegrationEvent>(_mapper.Map(domainEvent, trip));
        Assert.Equal(trip.Id, integrationEvent.TripId);
        Assert.Equal(TestPlanning.TenantId, integrationEvent.TenantId);
        Assert.Equal("merge:abc", integrationEvent.RoundTripKey);
        Assert.Equal("Outbound", integrationEvent.Direction);
    }

    [Fact]
    public void Round_trip_unpair_maps_null_key_and_direction()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-9004", clientId: Guid.NewGuid()).Value;
        trip.AssignRoundTrip("merge:abc", TripDirection.Outbound);
        trip.ClearDomainEvents();
        trip.ClearRoundTrip();
        var domainEvent = (TripRoundTripChangedDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripRoundTripChangedIntegrationEvent>(_mapper.Map(domainEvent, trip));
        Assert.Null(integrationEvent.RoundTripKey);
        Assert.Null(integrationEvent.Direction);
    }

    [Fact]
    public void Ready_for_billing_with_no_direction_maps_direction_to_null()
    {
        var trip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-9001", clientId: Guid.NewGuid(), direction: null).Value;
        trip.RecordPostTripInspection();
        trip.ClearDomainEvents();
        trip.FinishOperations();
        var domainEvent = (TripReadyForBillingDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripReadyForBillingIntegrationEvent>(_mapper.Map(domainEvent, trip));
        Assert.Null(integrationEvent.Direction);
    }

    [Fact]
    public void Driver_correction_maps_to_the_public_event_with_every_field()
    {
        var newDriverId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-9010", clientId: Guid.NewGuid()).Value;
        trip.RecordPostTripInspection();
        trip.FinishOperations();
        trip.ClearDomainEvents();
        Assert.True(trip.CorrectDriver(newDriverId, "J. Moose", "wrong driver", actorId, "dispatch@northernlink.ca").IsSuccess);
        var domainEvent = (TripDriverCorrectedDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripDriverCorrectedIntegrationEvent>(_mapper.Map(domainEvent, trip));

        Assert.Equal(trip.Id, integrationEvent.TripId);
        Assert.Equal(TestPlanning.TenantId, integrationEvent.TenantId);
        Assert.Equal("TR-9010", integrationEvent.TripNumber);
        Assert.Equal(new DateOnly(2026, 7, 21), integrationEvent.ServiceDate);
        Assert.Equal(TestPlanning.DriverId, integrationEvent.PreviousDriverId);
        Assert.Equal(TestPlanning.DriverName, integrationEvent.PreviousDriverName);
        Assert.Equal(newDriverId, integrationEvent.NewDriverId);
        Assert.Equal("J. Moose", integrationEvent.NewDriverName);
        Assert.Equal("wrong driver", integrationEvent.Reason);
        Assert.Equal(actorId, integrationEvent.CorrectedByUserId);
        Assert.Equal("dispatch@northernlink.ca", integrationEvent.CorrectedByEmail);
        Assert.Equal(domainEvent.OccurredAtUtc, integrationEvent.OccurredAtUtc);
        Assert.Equal("trips.trip-driver-corrected", Shared.EventBus.EventRoutingKey.For(typeof(TripDriverCorrectedIntegrationEvent)));
    }
}
