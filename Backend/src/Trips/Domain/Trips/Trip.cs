using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips.Events;

namespace NorthernLink.Trips.Domain.Trips;

/// <summary>
/// One dispatchable service leg on a service date — the row on the DispatchBoard. Trips
/// are either materialized from a <c>ScheduleTemplate</c> by the generation worker (which
/// stamps <see cref="ScheduleTemplateId"/>, <see cref="Direction"/>, and — for paired
/// outbound/return legs — a shared <see cref="RoundTripKey"/>) or created ad-hoc through
/// the wizard. Pairing is not creation-only: dispatchers can later merge two client trips
/// into a round trip (<see cref="MergeRoundTrip"/>), spin up an empty repositioning leg
/// (<see cref="CreateDeadheadReturn"/>), or undo a pairing (<see cref="ClearRoundTrip"/>).
/// Route and client fields are denormalized snapshots: editing a route or
/// template never rewrites history. Lifecycle is the <see cref="TripStatus"/> matrix;
/// "open — needs coverage" and "empty leg" are frontend derivations, not statuses.
/// <para>
/// The lifecycle is billing-driven past the point the bus stops.
/// <see cref="FinishOperations"/> is the dispatcher's "the run is over" signal; it lands a
/// client trip in <see cref="TripStatus.ReadyForBilling"/> (raising
/// <see cref="TripReadyForBillingDomainEvent"/>, Billing's feed) and a clientless one —
/// community runs and walk-up charters, whose fare was already taken — straight in
/// <see cref="TripStatus.Completed"/>. From there <see cref="MarkInvoiced"/>,
/// <see cref="MarkPaid"/>, and <see cref="WriteOff"/> are driven only by Billing's invoice
/// events, never by hand.
/// </para>
/// </summary>
public sealed class Trip : AggregateRoot, ITenantScoped
{
    private Trip()
    {
        // EF Core materialization only.
        TripNumber = null!;
        RouteName = null!;
        Origin = null!;
        Destination = null!;
    }

    public Guid TenantId { get; private set; }

    // Identity & when.
    public string TripNumber { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public TimeOnly WindowStart { get; private set; }
    public TimeOnly? WindowEnd { get; private set; }
    public TripServiceType ServiceType { get; private set; }

    // Route snapshot.
    public Guid? RouteId { get; private set; }
    public string RouteName { get; private set; }
    public string Origin { get; private set; }
    public string Destination { get; private set; }
    public List<RouteStop> Stops { get; private set; } = [];
    public int DistanceKm { get; private set; }

    // Template provenance.
    public Guid? ScheduleTemplateId { get; private set; }
    public string? RoundTripKey { get; private set; }
    public TripDirection? Direction { get; private set; }
    public bool IsEmptyLeg { get; private set; }

    // Client snapshot.
    public Guid? ClientId { get; private set; }
    public string? ClientName { get; private set; }
    public string? PoNumber { get; private set; }

    // Assignment.
    public Guid? DriverId { get; private set; }
    public string? DriverName { get; private set; }
    public Guid? VehicleId { get; private set; }
    public string? VehicleUnit { get; private set; }

    // Demand (gift-a-seat).
    public int? SeatsCapacity { get; private set; }
    public int SeatsConfirmed { get; private set; }
    public int? SeatsMinimum { get; private set; }
    public bool DemandGuaranteed { get; private set; }

    // Lifecycle.
    public TripStatus Status { get; private set; }
    public Guid? ManifestId { get; private set; }

    /// <summary>
    /// Set true once a post-trip vehicle inspection has been logged for this trip (Fleet's
    /// <c>fleet.vehicle-inspection-recorded</c> event, matched by trip number). Gates
    /// <see cref="FinishOperations"/>.
    /// </summary>
    public bool HasPostTripInspection { get; private set; }

    /// <summary>
    /// When the run itself ended — stamped by <see cref="FinishOperations"/> regardless of which
    /// status it lands in. Distinct from <see cref="CompletedAtUtc"/>, which now tracks payment:
    /// for a client trip the two are days or weeks apart, and this is the one that means
    /// "the bus got back".
    /// </summary>
    public DateTimeOffset? OperationsFinishedAtUtc { get; private set; }

    /// <summary>
    /// When the trip reached <see cref="TripStatus.Completed"/> — for a client trip that is the
    /// date payment was confirmed, for a clientless run the moment the run ended. Cleared by
    /// <see cref="MarkInvoiced"/> when a payment confirmation is undone, so it never claims a
    /// completion that has been walked back.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? CancelledReason { get; private set; }

    /// <summary>Why the trip will never be billed — required by <see cref="CloseWithoutBilling"/>,
    /// and copied from the invoice's reason when Billing writes one off.</summary>
    public string? WrittenOffReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// The run has happened (or been called off), so driver, vehicle, and demand are history.
    /// Replaces the old <c>IsTerminal</c>, which is no longer meaningful now that Completed can
    /// go back to Invoiced — "the bus is done with it" and "nothing can change" are different
    /// questions, and every assignment guard wants this one.
    /// </summary>
    public bool IsOperationallyClosed =>
        Status is not (TripStatus.Scheduled or TripStatus.InProgress);

    /// <summary>Genuinely final — no outgoing transitions at all.</summary>
    public bool IsFinal => Status is TripStatus.Cancelled or TripStatus.WrittenOff;

    /// <summary>
    /// The lifecycle transition matrix. Diagonal (same status) is not a transition.
    /// <para>
    /// Scheduled and InProgress both reach ReadyForBilling <em>and</em> Completed because
    /// <see cref="FinishOperations"/> picks the landing status from <see cref="ClientId"/>; the
    /// direct Scheduled → finish edges survive because dispatchers forget to press START and the
    /// real guard on finishing is the post-trip inspection, not the intermediate status.
    /// </para>
    /// <para>
    /// Completed → Invoiced is legal (a payment confirmation cleared in error), which makes this
    /// matrix insufficient on its own to stop a stale replay from un-completing a paid trip —
    /// <c>InvoiceBillingStateChangedIntegrationEventHandler</c> carries a high-water mark for
    /// that. Invoiced → ReadyForBilling is deliberately absent: once a worksheet is in
    /// QuickBooks it can be adjusted or written off, never un-sent.
    /// </para>
    /// </summary>
    public static bool CanTransition(TripStatus from, TripStatus to) =>
        (from, to) switch
        {
            (TripStatus.Scheduled, TripStatus.InProgress) => true,
            (TripStatus.Scheduled or TripStatus.InProgress,
                TripStatus.ReadyForBilling or TripStatus.Completed or TripStatus.Cancelled) => true,
            (TripStatus.ReadyForBilling, TripStatus.Invoiced or TripStatus.WrittenOff) => true,
            (TripStatus.Invoiced, TripStatus.Completed or TripStatus.WrittenOff) => true,
            (TripStatus.Completed, TripStatus.Invoiced) => true,
            _ => false,
        };

    /// <summary>
    /// The single creation path — used by the wizard (ad-hoc) and the generation worker
    /// (template-materialized, which passes the template provenance fields).
    /// </summary>
    public static Result<Trip> Schedule(
        Guid tenantId,
        string tripNumber,
        DateOnly serviceDate,
        TimeOnly windowStart,
        TimeOnly? windowEnd,
        TripServiceType serviceType,
        Guid? routeId,
        string routeName,
        string origin,
        string destination,
        IReadOnlyList<RouteStop> stops,
        int distanceKm,
        Guid? scheduleTemplateId,
        string? roundTripKey,
        TripDirection? direction,
        bool isEmptyLeg,
        Guid? clientId,
        string? clientName,
        string? poNumber,
        Guid? driverId,
        string? driverName,
        Guid? vehicleId,
        string? vehicleUnit,
        int? seatsCapacity,
        int? seatsMinimum)
    {
        if (string.IsNullOrWhiteSpace(tripNumber))
        {
            return Result.Failure<Trip>(TripErrors.TripNumberRequired);
        }

        var validation = ValidateDetails(routeName, origin, destination, distanceKm, seatsCapacity, seatsMinimum);
        if (validation.IsFailure)
        {
            return Result.Failure<Trip>(validation.Error);
        }

        if (driverId is not null && string.IsNullOrWhiteSpace(driverName))
        {
            return Result.Failure<Trip>(TripErrors.DriverNameRequired);
        }

        var now = DateTimeOffset.UtcNow;
        var trip = new Trip
        {
            TenantId = tenantId,
            TripNumber = tripNumber.Trim(),
            ServiceDate = serviceDate,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            ServiceType = serviceType,
            RouteId = routeId,
            RouteName = routeName.Trim(),
            Origin = origin.Trim(),
            Destination = destination.Trim(),
            Stops = [.. stops],
            DistanceKm = distanceKm,
            ScheduleTemplateId = scheduleTemplateId,
            RoundTripKey = Normalize(roundTripKey),
            Direction = direction,
            IsEmptyLeg = isEmptyLeg,
            ClientId = clientId,
            ClientName = Normalize(clientName),
            PoNumber = Normalize(poNumber),
            DriverId = driverId,
            DriverName = driverId is null ? null : driverName!.Trim(),
            VehicleId = vehicleId,
            VehicleUnit = Normalize(vehicleUnit),
            SeatsCapacity = seatsCapacity,
            SeatsConfirmed = 0,
            SeatsMinimum = seatsMinimum,
            DemandGuaranteed = false,
            Status = TripStatus.Scheduled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        trip.Raise(new TripScheduledDomainEvent(trip.Id));
        return Result.Success(trip);
    }

    /// <summary>
    /// Edits the trip's plan while it is still Scheduled. Assignment, demand, and status
    /// move through their own methods; the trip number and <see cref="ScheduleTemplateId"/>
    /// never change, and round-trip pairing moves only through
    /// <see cref="MergeRoundTrip"/>/<see cref="AssignRoundTrip"/>/<see cref="ClearRoundTrip"/>.
    /// </summary>
    public Result Update(
        DateOnly serviceDate,
        TimeOnly windowStart,
        TimeOnly? windowEnd,
        TripServiceType serviceType,
        Guid? routeId,
        string routeName,
        string origin,
        string destination,
        IReadOnlyList<RouteStop> stops,
        int distanceKm,
        bool isEmptyLeg,
        Guid? clientId,
        string? clientName,
        string? poNumber,
        int? seatsCapacity,
        int? seatsMinimum)
    {
        if (Status != TripStatus.Scheduled)
        {
            return Result.Failure(TripErrors.NotEditable);
        }

        var validation = ValidateDetails(routeName, origin, destination, distanceKm, seatsCapacity, seatsMinimum);
        if (validation.IsFailure)
        {
            return validation;
        }

        ServiceDate = serviceDate;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        ServiceType = serviceType;
        RouteId = routeId;
        RouteName = routeName.Trim();
        Origin = origin.Trim();
        Destination = destination.Trim();
        Stops = [.. stops];
        DistanceKm = distanceKm;
        IsEmptyLeg = isEmptyLeg;
        ClientId = clientId;
        ClientName = Normalize(clientName);
        PoNumber = Normalize(poNumber);
        // Manual capacity applies only to trips without a fleet vehicle — an assigned
        // vehicle's snapshotted capacity is server-authoritative and survives plan edits.
        SeatsCapacity = VehicleId is null ? seatsCapacity : SeatsCapacity;
        SeatsMinimum = seatsMinimum;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripUpdatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>Assigns a driver (existence + Active are validated against driver_lookup by the handler).</summary>
    public Result AssignDriver(Guid driverId, string driverName)
    {
        if (IsOperationallyClosed)
        {
            return Result.Failure(TripErrors.OperationallyClosed(Status));
        }

        if (string.IsNullOrWhiteSpace(driverName))
        {
            return Result.Failure(TripErrors.DriverNameRequired);
        }

        DriverId = driverId;
        DriverName = driverName.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripAssignmentChangedDomainEvent(Id));
        return Result.Success();
    }

    public Result UnassignDriver()
    {
        if (IsOperationallyClosed)
        {
            return Result.Failure(TripErrors.OperationallyClosed(Status));
        }

        DriverId = null;
        DriverName = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripAssignmentChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Sets (or, with nulls, clears) the vehicle working this trip. A supplied
    /// <paramref name="vehicleId"/> is validated against vehicle_lookup (exists + Active)
    /// by the handler, which passes the unit-number and seating-capacity snapshots from the
    /// lookup as <paramref name="vehicleUnit"/>/<paramref name="seatingCapacity"/> — so a
    /// fleet vehicle stamps <see cref="SeatsCapacity"/> the way it stamps the unit number
    /// (snapshot semantics; later Fleet edits never ripple back). Assignment is refused when
    /// the vehicle seats fewer than <see cref="SeatsConfirmed"/>. A free-form unit with no id
    /// is still allowed and — like an unassign — leaves <see cref="SeatsCapacity"/> alone:
    /// demand may already be booked against the last-known capacity.
    /// </summary>
    public Result AssignVehicle(Guid? vehicleId, string? vehicleUnit, int? seatingCapacity)
    {
        if (IsOperationallyClosed)
        {
            return Result.Failure(TripErrors.OperationallyClosed(Status));
        }

        if (vehicleId is not null)
        {
            if (seatingCapacity is { } capacity && SeatsConfirmed > capacity)
            {
                return Result.Failure(TripErrors.VehicleCapacityBelowConfirmed);
            }

            SeatsCapacity = seatingCapacity;
        }

        VehicleId = vehicleId;
        VehicleUnit = Normalize(vehicleUnit);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripAssignmentChangedDomainEvent(Id));
        return Result.Success();
    }

    public Result Start()
    {
        if (!CanTransition(Status, TripStatus.InProgress))
        {
            return Result.Failure(TripErrors.InvalidStatusTransition(Status, TripStatus.InProgress));
        }

        Status = TripStatus.InProgress;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripStatusChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// The dispatcher's "the run is over" signal, and the only path out of the operational half
    /// of the lifecycle. Where it lands depends on whether anyone will be invoiced:
    /// a client trip goes to <see cref="TripStatus.ReadyForBilling"/> and raises
    /// <see cref="TripReadyForBillingDomainEvent"/> (which Billing turns into a billable-trip
    /// row); a trip with no client — a community run or walk-up charter, whose fare was already
    /// collected at booking or by the dispatcher — goes straight to
    /// <see cref="TripStatus.Completed"/> and never enters the billing arc at all.
    /// <para>
    /// This is a command in its own right rather than a status target, because the caller cannot
    /// know which status it produces. The post-trip inspection gate is unchanged and is checked
    /// first, so finishing is refused via any path (including a direct Scheduled → finish).
    /// </para>
    /// </summary>
    public Result FinishOperations()
    {
        // Business rule: a trip can never leave the operational phase without a logged
        // post-trip inspection — checked before the transition so it is refused via any path.
        if (!HasPostTripInspection)
        {
            return Result.Failure(TripErrors.PostTripInspectionRequired);
        }

        var target = ClientId is null ? TripStatus.Completed : TripStatus.ReadyForBilling;
        if (!CanTransition(Status, target))
        {
            return Result.Failure(TripErrors.InvalidStatusTransition(Status, target));
        }

        var now = DateTimeOffset.UtcNow;
        Status = target;
        OperationsFinishedAtUtc = now;
        UpdatedAtUtc = now;

        if (target == TripStatus.Completed)
        {
            // No invoice will ever settle this one, so the run ending is the completion.
            CompletedAtUtc = now;
            Raise(new TripCompletedDomainEvent(Id));
        }
        else
        {
            Raise(new TripReadyForBillingDomainEvent(Id));
        }

        return Result.Success();
    }

    /// <summary>
    /// The dispatcher's escape for a run that will never be invoiced — a client with no active
    /// contract, a goodwill trip, a job written off before a worksheet was ever drafted. Without
    /// it <see cref="TripStatus.ReadyForBilling"/> would be a state with no exit, since every
    /// other way out of it is driven by an invoice that is never going to exist.
    /// <para>
    /// Lands in the same <see cref="TripStatus.WrittenOff"/> as an invoice write-off; the
    /// required reason is what distinguishes the two in the audit trail.
    /// </para>
    /// </summary>
    public Result CloseWithoutBilling(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(TripErrors.WriteOffReasonRequired);
        }

        if (!CanTransition(Status, TripStatus.WrittenOff))
        {
            return Result.Failure(TripErrors.InvalidStatusTransition(Status, TripStatus.WrittenOff));
        }

        Status = TripStatus.WrittenOff;
        WrittenOffReason = reason.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        // Not TripWrittenOffDomainEvent: this write-off originates here, so Billing has to be
        // told to drop the trip from its billable pool — the mapper publishes exactly this event.
        Raise(new TripClosedWithoutBillingDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Records that the trip's worksheet has been keyed into QuickBooks. Driven only by Billing's
    /// <c>billing.invoice-billing-state-changed</c> event — never settable by hand.
    /// <para>
    /// Also the path back from <see cref="TripStatus.Completed"/> when a payment confirmation is
    /// cleared in error, which is why it clears <see cref="CompletedAtUtc"/>. Idempotent: a
    /// re-delivery while already Invoiced is a no-op success raising nothing.
    /// </para>
    /// </summary>
    public Result MarkInvoiced()
    {
        if (Status == TripStatus.Invoiced)
        {
            return Result.Success();
        }

        var guard = GuardBillingDriven();
        if (guard.IsFailure)
        {
            return guard;
        }

        if (!CanTransition(Status, TripStatus.Invoiced))
        {
            return Result.Failure(TripErrors.InvalidStatusTransition(Status, TripStatus.Invoiced));
        }

        Status = TripStatus.Invoiced;
        CompletedAtUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripInvoicedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Payment against the trip's QuickBooks invoice has been confirmed — the trip is finally
    /// Completed. Driven only by Billing. Idempotent, like its siblings.
    /// </summary>
    public Result MarkPaid()
    {
        if (Status == TripStatus.Completed)
        {
            return Result.Success();
        }

        var guard = GuardBillingDriven();
        if (guard.IsFailure)
        {
            return guard;
        }

        if (!CanTransition(Status, TripStatus.Completed))
        {
            return Result.Failure(TripErrors.InvalidStatusTransition(Status, TripStatus.Completed));
        }

        Status = TripStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CompletedAtUtc.Value;

        Raise(new TripCompletedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// The invoice carrying this trip was written off — the money is not coming. Driven only by
    /// Billing. Final; the claim on the trip is deliberately never released, so a written-off
    /// trip can never be re-billed. Idempotent.
    /// </summary>
    public Result WriteOff(string? reason)
    {
        if (Status == TripStatus.WrittenOff)
        {
            return Result.Success();
        }

        var guard = GuardBillingDriven();
        if (guard.IsFailure)
        {
            return guard;
        }

        if (!CanTransition(Status, TripStatus.WrittenOff))
        {
            return Result.Failure(TripErrors.InvalidStatusTransition(Status, TripStatus.WrittenOff));
        }

        Status = TripStatus.WrittenOff;
        WrittenOffReason = Normalize(reason);
        CompletedAtUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripWrittenOffDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// A clientless run never enters the billing arc, so no invoice can ever speak for it —
    /// a billing-driven transition arriving for one means a claim set has gone wrong upstream.
    /// </summary>
    private Result GuardBillingDriven() =>
        ClientId is null
            ? Result.Failure(TripErrors.BillingStateOnClientlessTrip)
            : Result.Success();

    public Result Cancel(string? reason)
    {
        if (!CanTransition(Status, TripStatus.Cancelled))
        {
            return Result.Failure(TripErrors.InvalidStatusTransition(Status, TripStatus.Cancelled));
        }

        Status = TripStatus.Cancelled;
        CancelledReason = Normalize(reason);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripStatusChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Merges two existing client trips into one round trip: hard validation (distinct
    /// trips, same tenant, same client, same service date, mirrored corridors, neither
    /// Cancelled, neither already paired), then both legs get a fresh shared
    /// <see cref="RoundTripKey"/> ("merge:&lt;guid&gt;" — opaque, distinct from the template
    /// worker's "{templateId:N}:{yyyyMMdd}" format). Leg direction: when
    /// <paramref name="firstDirection"/> is provided (the caller resolved it from the trips'
    /// manifests), <paramref name="first"/> takes it and <paramref name="second"/> the
    /// opposite; otherwise the chronologically earlier leg — by (<see cref="ServiceDate"/>,
    /// <see cref="WindowStart"/>) — becomes Outbound (ties on both go to
    /// <paramref name="first"/>). Completed legs merge fine — that is the point:
    /// Billing re-keys its uninvoiced replica rows off the resulting events.
    /// <para>
    /// <paramref name="allowMismatch"/> is the dispatcher's manual override for returns the
    /// strict matcher can't see — a return leg on a different service date (overnight
    /// returns) or a corridor worded differently on each leg. It skips only the
    /// service-date-equality and mirrored-corridor checks; distinct trips, same tenant,
    /// same non-null client, not Cancelled, and not already paired stay non-negotiable —
    /// Billing groups pairs by <see cref="RoundTripKey"/> within a single client's contract
    /// invoice, so a cross-client or clientless pair can never be represented downstream.
    /// </para>
    /// </summary>
    public static Result MergeRoundTrip(
        Trip first,
        Trip second,
        bool allowMismatch = false,
        TripDirection? firstDirection = null)
    {
        if (first.Id == second.Id)
        {
            return Result.Failure(TripErrors.RoundTripSameTrip);
        }

        if (first.TenantId != second.TenantId)
        {
            return Result.Failure(TripErrors.RoundTripTenantMismatch);
        }

        if (first.ClientId is null || second.ClientId is null)
        {
            return Result.Failure(TripErrors.RoundTripClientRequired);
        }

        if (first.ClientId != second.ClientId)
        {
            return Result.Failure(TripErrors.RoundTripClientMismatch);
        }

        if (!allowMismatch)
        {
            if (first.ServiceDate != second.ServiceDate)
            {
                return Result.Failure(TripErrors.RoundTripServiceDateMismatch);
            }

            if (!CorridorsMirror(first, second))
            {
                return Result.Failure(TripErrors.RoundTripCorridorMismatch);
            }
        }

        // Completed and ReadyForBilling legs merge fine — that is the point. Final legs do not:
        // re-keying a written-off or cancelled leg would rewrite a settled pairing.
        if (first.IsFinal || second.IsFinal)
        {
            return Result.Failure(TripErrors.RoundTripFinal);
        }

        if (first.RoundTripKey is not null || second.RoundTripKey is not null)
        {
            return Result.Failure(TripErrors.RoundTripAlreadyPaired);
        }

        // Manifest-declared direction wins; chronological order across dates is the
        // fallback: manual pairs can span service dates (overnight returns), so compare
        // (ServiceDate, WindowStart), not time-of-day alone.
        var (outbound, inbound) = firstDirection switch
        {
            TripDirection.Outbound => (first, second),
            TripDirection.Inbound => (second, first),
            _ => (first.ServiceDate, first.WindowStart).CompareTo((second.ServiceDate, second.WindowStart)) <= 0
                ? (first, second)
                : (second, first),
        };

        var key = NewMergeRoundTripKey();
        var outboundResult = outbound.AssignRoundTrip(key, TripDirection.Outbound);
        if (outboundResult.IsFailure)
        {
            return outboundResult;
        }

        return inbound.AssignRoundTrip(key, TripDirection.Inbound);
    }

    /// <summary>
    /// Creates the empty repositioning leg for a client trip with no return: a NEW
    /// Scheduled trip on the reversed corridor (stops reversed and renumbered), same
    /// service date/client/distance/service type, departing at this trip's
    /// <see cref="WindowEnd"/> (or <see cref="WindowStart"/> when open-ended), no
    /// driver/vehicle/seats, <see cref="IsEmptyLeg"/> true and Inbound — while this trip
    /// becomes the Outbound leg of a fresh shared "merge:" <see cref="RoundTripKey"/>.
    /// The caller mints <paramref name="tripNumber"/> from the per-tenant sequence.
    /// </summary>
    public Result<Trip> CreateDeadheadReturn(string tripNumber)
    {
        if (ClientId is null)
        {
            return Result.Failure<Trip>(TripErrors.RoundTripClientRequired);
        }

        if (RoundTripKey is not null)
        {
            return Result.Failure<Trip>(TripErrors.RoundTripAlreadyPaired);
        }

        if (IsFinal)
        {
            return Result.Failure<Trip>(TripErrors.RoundTripFinal);
        }

        if (IsEmptyLeg)
        {
            return Result.Failure<Trip>(TripErrors.DeadheadReturnOfEmptyLeg);
        }

        var reversedStops = Stops
            .OrderByDescending(s => s.Order)
            .Select((stop, index) => new RouteStop
            {
                StopId = stop.StopId,
                Name = stop.Name,
                Order = index,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
            })
            .ToList();

        var key = NewMergeRoundTripKey();
        var returnTrip = Schedule(
            TenantId,
            tripNumber,
            ServiceDate,
            windowStart: WindowEnd ?? WindowStart,
            windowEnd: null,
            ServiceType,
            routeId: RouteId,
            RouteName,
            origin: Destination,
            destination: Origin,
            reversedStops,
            DistanceKm,
            scheduleTemplateId: null,
            roundTripKey: key,
            direction: TripDirection.Inbound,
            isEmptyLeg: true,
            ClientId,
            ClientName,
            PoNumber,
            driverId: null,
            driverName: null,
            vehicleId: null,
            vehicleUnit: null,
            seatsCapacity: null,
            seatsMinimum: null);

        if (returnTrip.IsFailure)
        {
            return returnTrip;
        }

        var paired = AssignRoundTrip(key, TripDirection.Outbound);
        return paired.IsFailure ? Result.Failure<Trip>(paired.Error) : returnTrip;
    }

    /// <summary>
    /// Stamps a round-trip pairing onto an existing trip — reachable only through
    /// <see cref="MergeRoundTrip"/>/<see cref="CreateDeadheadReturn"/>'s validation, but
    /// re-guards its own invariants (unpaired, not final) for defence in depth.
    /// </summary>
    public Result AssignRoundTrip(string roundTripKey, TripDirection direction)
    {
        if (string.IsNullOrWhiteSpace(roundTripKey))
        {
            return Result.Failure(TripErrors.RoundTripKeyRequired);
        }

        if (IsFinal)
        {
            return Result.Failure(TripErrors.RoundTripFinal);
        }

        if (RoundTripKey is not null)
        {
            return Result.Failure(TripErrors.RoundTripAlreadyPaired);
        }

        RoundTripKey = roundTripKey.Trim();
        Direction = direction;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripRoundTripChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Operational undo of a pairing: clears <see cref="RoundTripKey"/> and
    /// <see cref="Direction"/> (the handler clears both legs of the pair). Refused when
    /// the trip is not paired.
    /// </summary>
    public Result ClearRoundTrip()
    {
        if (RoundTripKey is null)
        {
            return Result.Failure(TripErrors.RoundTripNotPaired);
        }

        RoundTripKey = null;
        Direction = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripRoundTripChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>Records confirmed demand (Manifests screen; guaranteed = "gift-a-seat" pledge).</summary>
    public Result RecordDemand(int seatsConfirmed, bool demandGuaranteed)
    {
        if (IsOperationallyClosed)
        {
            return Result.Failure(TripErrors.OperationallyClosed(Status));
        }

        if (seatsConfirmed < 0)
        {
            return Result.Failure(TripErrors.InvalidSeats);
        }

        if (SeatsCapacity is { } capacity && seatsConfirmed > capacity)
        {
            return Result.Failure(TripErrors.SeatsExceedCapacity);
        }

        SeatsConfirmed = seatsConfirmed;
        DemandGuaranteed = demandGuaranteed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripDemandRecordedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Links the manifest recorded for this trip. Linking no longer changes trip status —
    /// a manifest can be recorded while the trip is Scheduled or InProgress, and completion
    /// stays the explicit <c>ChangeStatus → Complete</c> path. Idempotent: re-attaching the
    /// same manifest is a no-op success (the reaction pipeline is at-least-once); a
    /// different manifest is a conflict.
    /// </summary>
    public Result AttachManifest(Guid manifestId)
    {
        if (ManifestId == manifestId)
        {
            return Result.Success();
        }

        if (ManifestId is not null)
        {
            return Result.Failure(TripErrors.ManifestAlreadyAttached);
        }

        ManifestId = manifestId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripManifestLinkedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Records that a post-trip vehicle inspection has been logged for this trip — the signal
    /// that clears the <see cref="Complete"/> gate. Driven by Fleet's
    /// <c>fleet.vehicle-inspection-recorded</c> integration event, matched by trip number.
    /// Idempotent: once set, re-delivery is a no-op success (the reaction pipeline is
    /// at-least-once), so the flag never flips back and only one event is raised.
    /// </summary>
    public Result RecordPostTripInspection()
    {
        if (HasPostTripInspection)
        {
            return Result.Success();
        }

        HasPostTripInspection = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripPostTripInspectionRecordedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Clears the post-trip-inspection signal — the inverse of <see cref="RecordPostTripInspection"/>
    /// — re-arming the <see cref="Complete"/> gate after the trip's post-trip inspection is
    /// removed in Fleet. Driven by Fleet's <c>fleet.vehicle-inspection-removed</c> integration
    /// event, matched by trip number. Idempotent: if the flag is already clear this is a no-op
    /// success, so re-delivery raises nothing. No terminal guard — a removal can arrive after a
    /// trip was completed, and re-arming the gate on an already-Completed trip changes no status
    /// (Completed does not transition back); the flag simply reflects reality.
    /// </summary>
    public Result ClearPostTripInspection()
    {
        if (!HasPostTripInspection)
        {
            return Result.Success();
        }

        HasPostTripInspection = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripPostTripInspectionClearedDomainEvent(Id));
        return Result.Success();
    }

    private static Result ValidateDetails(
        string routeName,
        string origin,
        string destination,
        int distanceKm,
        int? seatsCapacity,
        int? seatsMinimum)
    {
        if (string.IsNullOrWhiteSpace(routeName))
        {
            return Result.Failure(TripErrors.RouteNameRequired);
        }

        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
        {
            return Result.Failure(TripErrors.OriginAndDestinationRequired);
        }

        if (distanceKm < 0)
        {
            return Result.Failure(TripErrors.InvalidDistance);
        }

        if (seatsCapacity is < 0 || seatsMinimum is < 0)
        {
            return Result.Failure(TripErrors.InvalidSeats);
        }

        return Result.Success();
    }

    /// <summary>Mirrored corridors: a.Origin==b.Destination and a.Destination==b.Origin, trimmed, case-insensitive.</summary>
    private static bool CorridorsMirror(Trip a, Trip b) =>
        string.Equals(a.Origin.Trim(), b.Destination.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Destination.Trim(), b.Origin.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opaque key for dispatcher-made pairs — the "merge:" prefix keeps it disjoint from
    /// the template worker's "{templateId:N}:{yyyyMMdd}" keys.
    /// </summary>
    private static string NewMergeRoundTripKey() => $"merge:{Guid.NewGuid():N}";

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
