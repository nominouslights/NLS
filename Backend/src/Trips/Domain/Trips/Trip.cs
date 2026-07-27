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
/// the wizard. Route and client fields are denormalized snapshots: editing a route or
/// template never rewrites history. Lifecycle is the <see cref="TripStatus"/> matrix;
/// "open — needs coverage" and "empty leg" are frontend derivations, not statuses.
/// Completion (explicit or via an attached manifest) raises
/// <see cref="TripCompletedDomainEvent"/>, Billing's feed.
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
    /// <see cref="Complete"/>.
    /// </summary>
    public bool HasPostTripInspection { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? CancelledReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsTerminal => Status is TripStatus.Completed or TripStatus.Cancelled;

    /// <summary>The lifecycle transition matrix. Diagonal (same status) is not a transition.</summary>
    public static bool CanTransition(TripStatus from, TripStatus to) =>
        (from, to) switch
        {
            (TripStatus.Scheduled, TripStatus.InProgress or TripStatus.Completed or TripStatus.Cancelled) => true,
            (TripStatus.InProgress, TripStatus.Completed or TripStatus.Cancelled) => true,
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
    /// move through their own methods; trip number and template provenance never change.
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
        SeatsCapacity = seatsCapacity;
        SeatsMinimum = seatsMinimum;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new TripUpdatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>Assigns a driver (existence + Active are validated against driver_lookup by the handler).</summary>
    public Result AssignDriver(Guid driverId, string driverName)
    {
        if (IsTerminal)
        {
            return Result.Failure(TripErrors.TerminalStatus(Status));
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
        if (IsTerminal)
        {
            return Result.Failure(TripErrors.TerminalStatus(Status));
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
    /// by the handler, which passes the unit-number snapshot from the lookup as
    /// <paramref name="vehicleUnit"/>. A free-form unit with no id is still allowed.
    /// </summary>
    public Result AssignVehicle(Guid? vehicleId, string? vehicleUnit)
    {
        if (IsTerminal)
        {
            return Result.Failure(TripErrors.TerminalStatus(Status));
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

    public Result Complete()
    {
        // Business rule: a trip can never reach Completed without a logged post-trip
        // inspection — checked before the transition so completion is refused via any path
        // (including a direct Scheduled → Completed).
        if (!HasPostTripInspection)
        {
            return Result.Failure(TripErrors.PostTripInspectionRequired);
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

    /// <summary>Records confirmed demand (Manifests screen; guaranteed = "gift-a-seat" pledge).</summary>
    public Result RecordDemand(int seatsConfirmed, bool demandGuaranteed)
    {
        if (IsTerminal)
        {
            return Result.Failure(TripErrors.TerminalStatus(Status));
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
