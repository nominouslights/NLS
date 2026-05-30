using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips.Events;

namespace ShuttleApi.Domain.Trips;

public sealed class Trip : AggregateRoot<Guid>
{
    private readonly List<TripStop> _stops = [];
    private readonly List<TripPassenger> _passengers = [];

    public Guid? ClientId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Guid? DriverId { get; private set; }
    public TripServiceType ServiceType { get; private set; }
    public string? PurchaseOrderNumber { get; private set; }
    public string? VehicleType { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public TripStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int? SeatCapacity { get; private set; }
    public decimal? PricePerSeat { get; private set; }

    public IReadOnlyList<TripStop> Stops => _stops.AsReadOnly();
    public IReadOnlyList<TripPassenger> Passengers => _passengers.AsReadOnly();
    public TripPreInspection? PreInspection { get; private set; }
    public TripPostReport? PostReport { get; private set; }

    private Trip() { }

    public static Trip Create(
        TripServiceType serviceType,
        Guid? clientId,
        Guid? vehicleId,
        string? purchaseOrderNumber,
        string? vehicleType,
        DateTime scheduledAt,
        string? notes,
        IEnumerable<(int SequenceOrder, string LocationName, string? Address)> stops,
        int? seatCapacity = null,
        decimal? pricePerSeat = null)
    {
        if (serviceType == TripServiceType.Charter && clientId is null)
            throw new InvalidOperationException("ClientId is required for Charter trips.");

        if (serviceType == TripServiceType.Charter && vehicleId is null)
            throw new InvalidOperationException("VehicleId is required for Charter trips.");

        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            ServiceType = serviceType,
            ClientId = clientId,
            VehicleId = vehicleId,
            PurchaseOrderNumber = purchaseOrderNumber,
            VehicleType = vehicleType,
            ScheduledAt = scheduledAt,
            Status = TripStatus.Scheduled,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            SeatCapacity = seatCapacity,
            PricePerSeat = pricePerSeat
        };

        foreach (var (seq, loc, addr) in stops)
            trip._stops.Add(TripStop.Create(trip.Id, seq, loc, addr));

        trip.RaiseDomainEvent(new TripCreatedEvent(trip.Id));

        return trip;
    }

    public void AssignVehicle(Guid vehicleId)
    {
        Guard.Against(ServiceType != TripServiceType.Community, "AssignVehicle is only for community trips.");
        VehicleId = vehicleId;
    }

    public void Update(
        Guid? vehicleId,
        string? purchaseOrderNumber,
        string? vehicleType,
        DateTime scheduledAt,
        string? notes,
        IEnumerable<(int SequenceOrder, string LocationName, string? Address)> stops,
        int? seatCapacity = null,
        decimal? pricePerSeat = null)
    {
        Guard.Against(Status != TripStatus.Scheduled, "Only scheduled trips can be updated.");

        if (vehicleId.HasValue) VehicleId = vehicleId.Value;
        PurchaseOrderNumber = purchaseOrderNumber;
        VehicleType = vehicleType;
        ScheduledAt = scheduledAt;
        Notes = notes;
        SeatCapacity = seatCapacity;
        PricePerSeat = pricePerSeat;

        _stops.Clear();
        foreach (var (seq, loc, addr) in stops)
            _stops.Add(TripStop.Create(Id, seq, loc, addr));
    }

    public TripPassenger AddPassenger(
        string name,
        string? contactInfo,
        int? seatNumber,
        PassengerPaymentStatus paymentStatus,
        string? bookingReference = null,
        string? phone = null,
        string? email = null,
        string? direction = null,
        DateTime? cutoffDeadline = null,
        DateTime? bookedAt = null,
        decimal? fare = null)
    {
        Guard.Against(ServiceType != TripServiceType.Community, "Passengers can only be added to Community trips.");

        var passenger = TripPassenger.Create(
            Id, name, contactInfo, seatNumber, paymentStatus,
            bookingReference, phone, email, direction, cutoffDeadline, bookedAt, fare);
        _passengers.Add(passenger);
        return passenger;
    }

    public void RemovePassenger(Guid passengerId)
    {
        Guard.Against(ServiceType != TripServiceType.Community, "Passengers can only be removed from Community trips.");

        var passenger = _passengers.FirstOrDefault(p => p.Id == passengerId)
            ?? throw new InvalidOperationException($"Passenger {passengerId} not found on this trip.");
        _passengers.Remove(passenger);
    }

    public void UpdatePassengerPaymentStatus(Guid passengerId, PassengerPaymentStatus status)
    {
        Guard.Against(ServiceType != TripServiceType.Community, "Passenger payment status can only be updated on Community trips.");

        var passenger = _passengers.FirstOrDefault(p => p.Id == passengerId)
            ?? throw new InvalidOperationException($"Passenger {passengerId} not found on this trip.");
        passenger.UpdatePaymentStatus(status);
    }

    public void AssignDriver(Guid driverId, string? vehicleType)
    {
        DriverId = driverId;
        if (vehicleType is not null)
            VehicleType = vehicleType;
    }

    public void Dispatch()
    {
        Guard.Against(DriverId is null, "A driver must be assigned before dispatching.");
        Guard.Against(VehicleId is null, "A vehicle must be assigned before dispatching.");
        Guard.Against(Status != TripStatus.Scheduled, "Only scheduled trips can be dispatched.");

        Status = TripStatus.Dispatched;
        RaiseDomainEvent(new TripDispatchedEvent(Id, DriverId!.Value));
    }

    public void UpdateStatus(TripStatus newStatus)
    {
        Guard.Against(
            newStatus != TripStatus.EnRoute && newStatus != TripStatus.Cancelled,
            "Status can only be updated to EnRoute or Cancelled via this method.");

        if (newStatus == TripStatus.EnRoute)
            Guard.Against(Status != TripStatus.Dispatched, "Trip must be dispatched before going en route.");

        if (newStatus == TripStatus.Cancelled)
            Guard.Against(
                Status != TripStatus.Scheduled && Status != TripStatus.Dispatched,
                "Only scheduled or dispatched trips can be cancelled.");

        Status = newStatus;
    }

    public void SubmitPreInspection(
        int odometerStart,
        IEnumerable<(string ItemName, bool Passed, string? Notes)> items)
    {
        Guard.Against(Status != TripStatus.Dispatched, "Pre-trip inspection can only be submitted for dispatched trips.");
        Guard.Against(PreInspection is not null, "A pre-trip inspection has already been submitted for this trip.");

        PreInspection = TripPreInspection.Create(Id, odometerStart, items);
    }

    public void SubmitPostReport(
        int odometerEnd,
        decimal? fuelAddedLitres,
        decimal? fuelCostDollars,
        bool hasIncident,
        IncidentType? incidentType,
        string? incidentDescription,
        string? additionalNotes,
        bool isReadyToInvoice)
    {
        Guard.Against(Status != TripStatus.EnRoute, "Post-trip report can only be submitted for trips en route.");
        Guard.Against(PostReport is not null, "A post-trip report has already been submitted for this trip.");
        Guard.Against(PreInspection is null, "A pre-trip inspection must be submitted before the post-trip report.");

        PostReport = TripPostReport.Create(
            Id,
            PreInspection!.OdometerStart,
            odometerEnd,
            fuelAddedLitres,
            fuelCostDollars,
            hasIncident,
            incidentType,
            incidentDescription,
            additionalNotes,
            isReadyToInvoice);

        Status = TripStatus.Completed;
        RaiseDomainEvent(new TripCompletedEvent(Id));
    }
}
