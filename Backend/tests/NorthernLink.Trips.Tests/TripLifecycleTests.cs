using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class TripLifecycleTests
{
    /// <summary>
    /// The complete allowed edge set — 12 edges, everything else forbidden. Kept as an explicit
    /// literal so a change to <see cref="Trip.CanTransition"/> has to be mirrored here on
    /// purpose, and checked as a full cross-product so a newly added enum member fails loudly
    /// instead of landing in the matrix's discard arm.
    /// </summary>
    private static readonly HashSet<(TripStatus From, TripStatus To)> AllowedEdges =
    [
        (TripStatus.Scheduled, TripStatus.InProgress),
        (TripStatus.Scheduled, TripStatus.ReadyForBilling),
        (TripStatus.Scheduled, TripStatus.Completed),
        (TripStatus.Scheduled, TripStatus.Cancelled),
        (TripStatus.InProgress, TripStatus.ReadyForBilling),
        (TripStatus.InProgress, TripStatus.Completed),
        (TripStatus.InProgress, TripStatus.Cancelled),
        (TripStatus.ReadyForBilling, TripStatus.Invoiced),
        (TripStatus.ReadyForBilling, TripStatus.WrittenOff),
        (TripStatus.Invoiced, TripStatus.Completed),
        (TripStatus.Invoiced, TripStatus.WrittenOff),
        (TripStatus.Completed, TripStatus.Invoiced),
    ];

    [Fact]
    public void Transition_matrix_matches_the_allowed_edge_set_exhaustively()
    {
        foreach (var from in Enum.GetValues<TripStatus>())
        {
            foreach (var to in Enum.GetValues<TripStatus>())
            {
                if (from == to)
                {
                    continue; // the diagonal is not a transition
                }

                Assert.True(
                    AllowedEdges.Contains((from, to)) == Trip.CanTransition(from, to),
                    $"CanTransition({from}, {to}) disagrees with the allowed edge set.");
            }
        }
    }

    [Fact]
    public void Invoiced_can_never_go_back_to_ready_for_billing()
    {
        // The headline rule of the billing-driven lifecycle: once a worksheet is in QuickBooks
        // it can be adjusted or written off, never un-sent — so the trip never walks back.
        Assert.False(Trip.CanTransition(TripStatus.Invoiced, TripStatus.ReadyForBilling));
    }

    [Fact]
    public void Schedule_creates_a_scheduled_trip_and_raises_the_scheduled_event()
    {
        var result = TestPlanning.ScheduleTrip();

        Assert.True(result.IsSuccess);
        var trip = result.Value;
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, trip.SeatsConfirmed);
        var scheduled = Assert.IsType<TripScheduledDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Equal(trip.Id, scheduled.TripId);
    }

    [Fact]
    public void Finish_on_a_clientless_trip_completes_it_and_raises_the_completed_event()
    {
        var trip = TestPlanning.ScheduleTrip().Value; // no client — community/walk-up shape
        trip.RecordPostTripInspection();
        trip.ClearDomainEvents();

        var result = trip.FinishOperations();

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.NotNull(trip.OperationsFinishedAtUtc);
        Assert.Equal(trip.OperationsFinishedAtUtc, trip.CompletedAtUtc); // the run ending IS the completion
        var completed = Assert.IsType<TripCompletedDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Equal(trip.Id, completed.TripId);
    }

    [Fact]
    public void Finish_on_a_client_trip_lands_in_ready_for_billing_and_raises_billings_feed()
    {
        var trip = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;
        trip.RecordPostTripInspection();
        trip.ClearDomainEvents();

        var result = trip.FinishOperations();

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.ReadyForBilling, trip.Status);
        Assert.NotNull(trip.OperationsFinishedAtUtc);
        Assert.Null(trip.CompletedAtUtc); // the money has not arrived
        var ready = Assert.IsType<TripReadyForBillingDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Equal(trip.Id, ready.TripId);
    }

    [Fact]
    public void Finish_on_a_deadhead_needs_no_inspection_and_lands_in_ready_for_billing()
    {
        // Built the way real deadheads are — off a client trip — so the leg carries the
        // client, the pairing key, and IsEmptyLeg exactly as production data would.
        var outbound = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;
        var deadhead = outbound.CreateDeadheadReturn("TR-1002").Value;
        deadhead.ClearDomainEvents();

        // No inspection recorded, no driver, still Scheduled: the empty leg is exempt from the
        // inspection gate (its inspection belongs to the trip the vehicle actually worked) and
        // the direct Scheduled -> finish edge carries it straight to Billing's doorstep.
        var result = deadhead.FinishOperations();

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.ReadyForBilling, deadhead.Status);
        Assert.NotNull(deadhead.OperationsFinishedAtUtc);
        Assert.Null(deadhead.CompletedAtUtc);
        var ready = Assert.IsType<TripReadyForBillingDomainEvent>(Assert.Single(deadhead.DomainEvents));
        Assert.Equal(deadhead.Id, ready.TripId);
    }

    [Fact]
    public void Deadhead_inspection_exemption_does_not_leak_to_regular_trips()
    {
        var regular = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;

        var result = regular.FinishOperations();

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PostTripInspectionRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, regular.Status);
    }

    [Fact]
    public void Finish_on_a_clientless_empty_leg_completes_it_without_an_inspection()
    {
        var trip = TestPlanning.ScheduleTrip(isEmptyLeg: true).Value; // no client
        trip.ClearDomainEvents();

        var result = trip.FinishOperations();

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.IsType<TripCompletedDomainEvent>(Assert.Single(trip.DomainEvents));
    }

    [Fact]
    public void Finish_is_rejected_without_a_logged_post_trip_inspection()
    {
        var trip = TestPlanning.ScheduleTrip().Value;

        var result = trip.FinishOperations();

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PostTripInspectionRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status); // refused via the direct Scheduled -> finish path
        Assert.Null(trip.OperationsFinishedAtUtc);
        Assert.Null(trip.CompletedAtUtc);
    }

    [Fact]
    public void Finish_is_allowed_once_a_post_trip_inspection_is_recorded()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        Assert.True(trip.FinishOperations().IsFailure); // gated before

        Assert.True(trip.RecordPostTripInspection().IsSuccess);
        var result = trip.FinishOperations();

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Status);
    }

    [Fact]
    public void Record_post_trip_inspection_is_idempotent_and_raises_one_event()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.ClearDomainEvents();

        Assert.True(trip.RecordPostTripInspection().IsSuccess);
        Assert.True(trip.HasPostTripInspection);
        Assert.Single(trip.DomainEvents.OfType<TripPostTripInspectionRecordedDomainEvent>());

        var second = trip.RecordPostTripInspection(); // redelivery converges

        Assert.True(second.IsSuccess);
        Assert.True(trip.HasPostTripInspection);
        Assert.Single(trip.DomainEvents.OfType<TripPostTripInspectionRecordedDomainEvent>()); // no second event
    }

    [Fact]
    public void Clearing_the_post_trip_inspection_re_arms_the_finish_gate()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        Assert.True(trip.RecordPostTripInspection().IsSuccess);
        Assert.True(trip.HasPostTripInspection);
        trip.ClearDomainEvents();

        var cleared = trip.ClearPostTripInspection();

        Assert.True(cleared.IsSuccess);
        Assert.False(trip.HasPostTripInspection);
        Assert.Single(trip.DomainEvents.OfType<TripPostTripInspectionClearedDomainEvent>());

        // The gate is re-armed: FinishOperations refuses again until a fresh post-trip DVIR is logged.
        var finish = trip.FinishOperations();
        Assert.True(finish.IsFailure);
        Assert.Equal(TripErrors.PostTripInspectionRequired, finish.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
    }

    [Fact]
    public void Clear_post_trip_inspection_is_an_idempotent_no_op_when_already_clear()
    {
        var trip = TestPlanning.ScheduleTrip().Value; // flag starts false
        Assert.False(trip.HasPostTripInspection);
        trip.ClearDomainEvents();

        var result = trip.ClearPostTripInspection();

        Assert.True(result.IsSuccess);
        Assert.False(trip.HasPostTripInspection);
        Assert.Empty(trip.DomainEvents.OfType<TripPostTripInspectionClearedDomainEvent>()); // nothing raised
    }

    [Fact]
    public void Start_then_finish_walks_the_happy_path()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.RecordPostTripInspection();

        Assert.True(trip.Start().IsSuccess);
        Assert.Equal(TripStatus.InProgress, trip.Status);
        Assert.True(trip.FinishOperations().IsSuccess);
    }

    [Fact]
    public void Start_on_an_in_progress_trip_is_rejected()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.Start();

        var result = trip.Start();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    // --- The billing-driven arc: MarkInvoiced / MarkPaid / WriteOff -----------------------

    private static Trip ReadyForBillingTrip()
    {
        var trip = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;
        trip.RecordPostTripInspection();
        Assert.True(trip.FinishOperations().IsSuccess);
        Assert.Equal(TripStatus.ReadyForBilling, trip.Status);
        trip.ClearDomainEvents();
        return trip;
    }

    [Fact]
    public void Mark_invoiced_then_paid_walks_the_billing_arc()
    {
        var trip = ReadyForBillingTrip();

        Assert.True(trip.MarkInvoiced().IsSuccess);
        Assert.Equal(TripStatus.Invoiced, trip.Status);
        Assert.Single(trip.DomainEvents.OfType<TripInvoicedDomainEvent>());

        Assert.True(trip.MarkPaid().IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.NotNull(trip.CompletedAtUtc);
        Assert.Single(trip.DomainEvents.OfType<TripCompletedDomainEvent>());
    }

    [Fact]
    public void Clearing_a_payment_walks_completed_back_to_invoiced_and_clears_the_timestamp()
    {
        var trip = ReadyForBillingTrip();
        trip.MarkInvoiced();
        trip.MarkPaid();
        Assert.NotNull(trip.CompletedAtUtc);

        var result = trip.MarkInvoiced(); // payment confirmation cleared in error

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Invoiced, trip.Status);
        Assert.Null(trip.CompletedAtUtc); // never claims a completion that was walked back
    }

    [Fact]
    public void System_transitions_are_idempotent_and_raise_no_second_event()
    {
        var trip = ReadyForBillingTrip();

        Assert.True(trip.MarkInvoiced().IsSuccess);
        Assert.True(trip.MarkInvoiced().IsSuccess); // at-least-once redelivery
        Assert.Single(trip.DomainEvents.OfType<TripInvoicedDomainEvent>());

        Assert.True(trip.MarkPaid().IsSuccess);
        Assert.True(trip.MarkPaid().IsSuccess);
        Assert.Single(trip.DomainEvents.OfType<TripCompletedDomainEvent>());

        var writtenOff = ReadyForBillingTrip();
        writtenOff.MarkInvoiced();
        Assert.True(writtenOff.WriteOff("Client defaulted").IsSuccess);
        Assert.True(writtenOff.WriteOff("Client defaulted").IsSuccess);
        Assert.Single(writtenOff.DomainEvents.OfType<TripWrittenOffDomainEvent>());
    }

    [Fact]
    public void System_transitions_reject_a_clientless_trip()
    {
        var trip = TestPlanning.ScheduleTrip().Value; // no client — never enters the billing arc
        trip.RecordPostTripInspection();

        Assert.Equal(TripErrors.BillingStateOnClientlessTrip, trip.MarkInvoiced().Error);
        Assert.Equal(TripErrors.BillingStateOnClientlessTrip, trip.MarkPaid().Error);
        Assert.Equal(TripErrors.BillingStateOnClientlessTrip, trip.WriteOff("nope").Error);
    }

    [Fact]
    public void Write_off_records_the_invoices_reason_and_is_final()
    {
        var trip = ReadyForBillingTrip();
        trip.MarkInvoiced();

        Assert.True(trip.WriteOff("Client insolvent").IsSuccess);
        Assert.Equal(TripStatus.WrittenOff, trip.Status);
        Assert.Equal("Client insolvent", trip.WrittenOffReason);
        Assert.True(trip.IsFinal);

        Assert.True(trip.MarkInvoiced().IsFailure); // no way out
        Assert.True(trip.MarkPaid().IsFailure);
    }

    [Fact]
    public void Close_without_billing_needs_a_reason_and_raises_its_own_event()
    {
        var trip = ReadyForBillingTrip();

        Assert.Equal(TripErrors.WriteOffReasonRequired, trip.CloseWithoutBilling(" ").Error);

        var result = trip.CloseWithoutBilling("Client has no active contract");

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.WrittenOff, trip.Status);
        Assert.Equal("Client has no active contract", trip.WrittenOffReason);
        // Its own event, not TripWrittenOffDomainEvent — this write-off is news Billing hasn't
        // heard, and the mapper publishes exactly this one.
        Assert.Single(trip.DomainEvents.OfType<TripClosedWithoutBillingDomainEvent>());
        Assert.Empty(trip.DomainEvents.OfType<TripWrittenOffDomainEvent>());
    }

    [Fact]
    public void Close_without_billing_is_refused_before_the_run_is_over()
    {
        var trip = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;

        var result = trip.CloseWithoutBilling("too early");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
    }

    // --- Operational closure ---------------------------------------------------------------

    [Fact]
    public void Final_trips_reject_further_operational_transitions()
    {
        var completed = TestPlanning.ScheduleTrip().Value;
        completed.RecordPostTripInspection();
        completed.FinishOperations();
        Assert.True(completed.Start().IsFailure);
        Assert.True(completed.FinishOperations().IsFailure);
        Assert.True(completed.Cancel(null).IsFailure);

        var cancelled = TestPlanning.ScheduleTrip().Value;
        cancelled.Cancel("Weather");
        Assert.True(cancelled.Start().IsFailure);
        Assert.True(cancelled.FinishOperations().IsFailure);
        Assert.Equal("Weather", cancelled.CancelledReason);
    }

    [Fact]
    public void Update_is_only_allowed_while_scheduled()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.Start();

        var result = trip.Update(
            trip.ServiceDate, trip.WindowStart, trip.WindowEnd, trip.ServiceType,
            trip.RouteId, trip.RouteName, trip.Origin, trip.Destination, trip.Stops,
            trip.DistanceKm, trip.IsEmptyLeg, trip.ClientId, trip.ClientName,
            trip.PoNumber, trip.SeatsCapacity, trip.SeatsMinimum);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotEditable, result.Error);
    }

    [Fact]
    public void Assign_and_unassign_driver_snapshot_the_name()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        var driverId = Guid.NewGuid();

        Assert.True(trip.AssignDriver(driverId, "R. Ballantyne").IsSuccess);
        Assert.Equal(driverId, trip.DriverId);
        Assert.Equal("R. Ballantyne", trip.DriverName);

        Assert.True(trip.UnassignDriver().IsSuccess);
        Assert.Null(trip.DriverId);
        Assert.Null(trip.DriverName);
    }

    [Fact]
    public void Assignment_is_rejected_once_the_run_is_over()
    {
        // The run has happened — driver, vehicle, and demand are history in EVERY post-run
        // status, not just the final ones. ReadyForBilling and Invoiced block edits too.
        var readyForBilling = ReadyForBillingTrip();
        Assert.True(readyForBilling.IsOperationallyClosed);
        Assert.Equal(TripErrors.OperationallyClosed(TripStatus.ReadyForBilling), readyForBilling.AssignDriver(Guid.NewGuid(), "R. Ballantyne").Error);
        Assert.True(readyForBilling.RecordDemand(3, demandGuaranteed: false).IsFailure);

        var invoiced = ReadyForBillingTrip();
        invoiced.MarkInvoiced();
        Assert.True(invoiced.AssignVehicle(Guid.NewGuid(), "U-07", seatingCapacity: 24).IsFailure);

        var completed = TestPlanning.ScheduleTrip().Value;
        completed.RecordPostTripInspection();
        completed.FinishOperations();
        Assert.True(completed.AssignDriver(Guid.NewGuid(), "R. Ballantyne").IsFailure);
        Assert.True(completed.UnassignDriver().IsFailure);
        Assert.True(completed.AssignVehicle(Guid.NewGuid(), "U-07", seatingCapacity: 24).IsFailure);
    }

    [Fact]
    public void Assign_vehicle_snapshots_the_capacity_and_reassign_re_snapshots_it()
    {
        var trip = TestPlanning.ScheduleTrip(seatsCapacity: 12).Value;

        // A fleet vehicle stamps SeatsCapacity the way it stamps the unit number.
        Assert.True(trip.AssignVehicle(Guid.NewGuid(), "U-07", seatingCapacity: 24).IsSuccess);
        Assert.Equal(24, trip.SeatsCapacity);

        Assert.True(trip.AssignVehicle(Guid.NewGuid(), "U-08", seatingCapacity: 48).IsSuccess);
        Assert.Equal(48, trip.SeatsCapacity);
    }

    [Fact]
    public void Unassign_and_free_form_unit_keep_the_last_known_capacity()
    {
        var trip = TestPlanning.ScheduleTrip(seatsCapacity: 12).Value;
        Assert.True(trip.AssignVehicle(Guid.NewGuid(), "U-07", seatingCapacity: 24).IsSuccess);

        // Demand may already be booked against the snapshot, so clearing the vehicle keeps it.
        Assert.True(trip.AssignVehicle(null, null, null).IsSuccess);
        Assert.Null(trip.VehicleId);
        Assert.Equal(24, trip.SeatsCapacity);

        // Same for a free-form unit with no id — there is no vehicle to derive from.
        Assert.True(trip.AssignVehicle(null, "U-99", null).IsSuccess);
        Assert.Equal("U-99", trip.VehicleUnit);
        Assert.Equal(24, trip.SeatsCapacity);
    }

    [Fact]
    public void Assigning_a_vehicle_seating_fewer_than_confirmed_demand_is_rejected()
    {
        var trip = TestPlanning.ScheduleTrip(seatsCapacity: 30).Value;
        Assert.True(trip.RecordDemand(28, demandGuaranteed: false).IsSuccess);

        var result = trip.AssignVehicle(Guid.NewGuid(), "U-07", seatingCapacity: 24);

        Assert.Equal(TripErrors.VehicleCapacityBelowConfirmed, result.Error);
        Assert.Null(trip.VehicleId); // the assignment never landed
        Assert.Equal(30, trip.SeatsCapacity);
    }

    [Fact]
    public void Update_keeps_derived_capacity_with_a_vehicle_and_applies_manual_without_one()
    {
        // While a fleet vehicle is assigned, the snapshotted capacity survives plan edits.
        var withVehicle = TestPlanning.ScheduleTrip(seatsCapacity: 12).Value;
        Assert.True(withVehicle.AssignVehicle(Guid.NewGuid(), "U-07", seatingCapacity: 24).IsSuccess);
        Assert.True(Replan(withVehicle, seatsCapacity: 10).IsSuccess);
        Assert.Equal(24, withVehicle.SeatsCapacity);

        // Without one, the manual capacity edit applies as before.
        var withoutVehicle = TestPlanning.ScheduleTrip(seatsCapacity: 12).Value;
        Assert.True(Replan(withoutVehicle, seatsCapacity: 10).IsSuccess);
        Assert.Equal(10, withoutVehicle.SeatsCapacity);
    }

    private static Result Replan(Trip trip, int? seatsCapacity) =>
        trip.Update(
            trip.ServiceDate, trip.WindowStart, trip.WindowEnd, trip.ServiceType,
            trip.RouteId, trip.RouteName, trip.Origin, trip.Destination, trip.Stops,
            trip.DistanceKm, trip.IsEmptyLeg, trip.ClientId, trip.ClientName,
            trip.PoNumber, seatsCapacity, trip.SeatsMinimum);

    [Fact]
    public void Demand_is_recorded_within_capacity()
    {
        var trip = TestPlanning.ScheduleTrip(seatsCapacity: 12).Value;

        Assert.True(trip.RecordDemand(9, demandGuaranteed: true).IsSuccess);
        Assert.Equal(9, trip.SeatsConfirmed);
        Assert.True(trip.DemandGuaranteed);

        var overCapacity = trip.RecordDemand(13, demandGuaranteed: false);
        Assert.Equal(TripErrors.SeatsExceedCapacity, overCapacity.Error);

        var negative = trip.RecordDemand(-1, demandGuaranteed: false);
        Assert.Equal(TripErrors.InvalidSeats, negative.Error);
    }

    [Fact]
    public void Attach_manifest_links_without_changing_status()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.ClearDomainEvents();
        var manifestId = Guid.NewGuid();

        var result = trip.AttachManifest(manifestId);

        Assert.True(result.IsSuccess);
        Assert.Equal(manifestId, trip.ManifestId);
        Assert.Equal(TripStatus.Scheduled, trip.Status); // linking never finishes the run
        Assert.Empty(trip.DomainEvents.OfType<TripCompletedDomainEvent>());
        Assert.Single(trip.DomainEvents.OfType<TripManifestLinkedDomainEvent>());
    }

    [Fact]
    public void Attach_manifest_is_idempotent_for_the_same_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        var manifestId = Guid.NewGuid();
        trip.AttachManifest(manifestId);
        trip.ClearDomainEvents();

        var result = trip.AttachManifest(manifestId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Empty(trip.DomainEvents); // no second link event
    }

    [Fact]
    public void Attach_manifest_rejects_a_second_different_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(Guid.NewGuid());

        var result = trip.AttachManifest(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.ManifestAlreadyAttached, result.Error);
    }
}
