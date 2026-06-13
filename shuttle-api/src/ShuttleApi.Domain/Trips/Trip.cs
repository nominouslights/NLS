using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips.Events;

namespace ShuttleApi.Domain.Trips;

public sealed class Trip : AggregateRoot<Guid>
{
    private readonly List<TripStop> _stops = [];
    private readonly List<TripPassenger> _passengers = [];
    private readonly List<TripCargoItem> _cargoItems = [];

    public Guid? ClientId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Guid? DriverId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public TripServiceType ServiceType { get; private set; }
    public string? PurchaseOrderNumber { get; private set; }
    public string? VehicleType { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public TripStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int? SeatCapacity { get; private set; }
    public decimal? PricePerSeat { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public bool IsDeadhead { get; private set; }
    public bool IsDeadheadBillable { get; private set; }

    public bool HasManifest => _passengers.Count > 0 || _cargoItems.Count > 0;

    public IReadOnlyList<TripStop> Stops => _stops.AsReadOnly();
    public IReadOnlyList<TripPassenger> Passengers => _passengers.AsReadOnly();
    public IReadOnlyList<TripCargoItem> CargoItems => _cargoItems.AsReadOnly();
    public TripPreInspection? PreInspection { get; private set; }
    public TripPostReport? PostReport { get; private set; }

    private Trip() { }

    public static Trip Create(
        TripServiceType serviceType,
        Guid? clientId,
        Guid? vehicleId,
        Guid? purchaseOrderId,
        string? purchaseOrderNumber,
        string? vehicleType,
        DateTime scheduledAt,
        string? notes,
        IEnumerable<(int SequenceOrder, string LocationName, string? Address)> stops,
        int? seatCapacity = null,
        decimal? pricePerSeat = null,
        bool isDeadhead = false,
        bool isDeadheadBillable = false)
    {
        if (isDeadheadBillable && !isDeadhead)
            throw new InvalidOperationException("Billable flag only applies to deadhead trips.");

        if (serviceType == TripServiceType.Charter && clientId is null)
            throw new InvalidOperationException("ClientId is required for Charter trips.");

        if (serviceType == TripServiceType.Charter && vehicleId is null)
            throw new InvalidOperationException("VehicleId is required for Charter trips.");

        if (serviceType == TripServiceType.Community && (purchaseOrderId is not null || purchaseOrderNumber is not null))
            throw new InvalidOperationException("Purchase orders are not supported for Community trips.");

        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            ServiceType = serviceType,
            ClientId = clientId,
            VehicleId = vehicleId,
            PurchaseOrderId = purchaseOrderId,
            PurchaseOrderNumber = purchaseOrderNumber,
            VehicleType = vehicleType,
            ScheduledAt = scheduledAt,
            Status = TripStatus.Scheduled,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            SeatCapacity = seatCapacity,
            PricePerSeat = pricePerSeat,
            IsDeadhead = isDeadhead,
            IsDeadheadBillable = isDeadhead && isDeadheadBillable
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
        Guid? purchaseOrderId,
        string? purchaseOrderNumber,
        string? vehicleType,
        DateTime scheduledAt,
        string? notes,
        IEnumerable<(int SequenceOrder, string LocationName, string? Address)> stops,
        int? seatCapacity = null,
        decimal? pricePerSeat = null,
        bool isDeadhead = false,
        bool isDeadheadBillable = false)
    {
        Guard.Against(Status != TripStatus.Scheduled, "Only scheduled trips can be updated.");

        if (isDeadheadBillable && !isDeadhead)
            throw new InvalidOperationException("Billable flag only applies to deadhead trips.");

        if (ServiceType == TripServiceType.Community && (purchaseOrderId is not null || purchaseOrderNumber is not null))
            throw new InvalidOperationException("Purchase orders are not supported for Community trips.");

        if (vehicleId.HasValue) VehicleId = vehicleId.Value;
        PurchaseOrderId = purchaseOrderId;
        PurchaseOrderNumber = purchaseOrderNumber;
        VehicleType = vehicleType;
        ScheduledAt = scheduledAt;
        Notes = notes;
        SeatCapacity = seatCapacity;
        PricePerSeat = pricePerSeat;
        IsDeadhead = isDeadhead;
        IsDeadheadBillable = isDeadhead && isDeadheadBillable;

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
        decimal? fare = null,
        bool isAddedAfterDeparture = false)
    {
        var passenger = TripPassenger.Create(
            Id, name, contactInfo, seatNumber, paymentStatus,
            bookingReference, phone, email, direction, cutoffDeadline, bookedAt, fare,
            isAddedAfterDeparture);
        _passengers.Add(passenger);
        return passenger;
    }

    public void RemovePassenger(Guid passengerId)
    {
        var passenger = _passengers.FirstOrDefault(p => p.Id == passengerId)
            ?? throw new InvalidOperationException($"Passenger {passengerId} not found on this trip.");
        _passengers.Remove(passenger);
    }

    public void UpdatePassengerPaymentStatus(Guid passengerId, PassengerPaymentStatus status)
    {
        var passenger = _passengers.FirstOrDefault(p => p.Id == passengerId)
            ?? throw new InvalidOperationException($"Passenger {passengerId} not found on this trip.");
        passenger.UpdatePaymentStatus(status);
    }

    public void UpdatePassengerBoardingStatus(Guid passengerId, PassengerBoardingStatus status)
    {
        var passenger = _passengers.FirstOrDefault(p => p.Id == passengerId)
            ?? throw new InvalidOperationException($"Passenger {passengerId} not found on this trip.");
        passenger.UpdateBoardingStatus(status);
    }

    public TripCargoItem AddCargoItem(
        CargoType cargoType,
        string? description,
        int quantity,
        decimal? weightKg = null,
        decimal? charge = null,
        bool isHazmat = false,
        bool isSecured = false)
    {
        var item = TripCargoItem.Create(Id, cargoType, description, quantity, weightKg, charge, isHazmat, isSecured);
        _cargoItems.Add(item);
        return item;
    }

    public void RemoveCargoItem(Guid cargoItemId)
    {
        var item = _cargoItems.FirstOrDefault(c => c.Id == cargoItemId)
            ?? throw new InvalidOperationException($"Cargo item {cargoItemId} not found on this trip.");
        _cargoItems.Remove(item);
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
        Guard.Against(
            !IsDeadhead && !HasManifest,
            "A trip must have at least one passenger or cargo item before dispatching. Mark the trip as a deadhead trip to dispatch without a manifest.");

        Status = TripStatus.Dispatched;
        RaiseDomainEvent(new TripDispatchedEvent(Id, DriverId!.Value));
    }

    public void UpdateStatus(TripStatus newStatus)
    {
        Guard.Against(
            newStatus != TripStatus.EnRoute && newStatus != TripStatus.Cancelled,
            "Status can only be updated to EnRoute or Cancelled via this method.");

        if (newStatus == TripStatus.EnRoute)
        {
            Guard.Against(Status != TripStatus.Dispatched, "Trip must be dispatched before going en route.");
            Guard.Against(PreInspection is null, "Pre-trip inspection must be completed before the trip can go en route.");
        }

        if (newStatus == TripStatus.Cancelled)
            Guard.Against(
                Status != TripStatus.Scheduled && Status != TripStatus.Dispatched,
                "Only scheduled or dispatched trips can be cancelled.");

        Status = newStatus;
    }

    public void SubmitPreInspection(
        int odometerStart,
        FuelLevel fuelLevel,
        string? weatherType,
        string? temperature,
        string? roadConditions,
        string? visibility,
        string? roadAdvisories,
        DateTime? weatherPulledAt,
        IEnumerable<(string ItemName, InspectionCategory Category, bool Passed, string? Notes)> items)
    {
        Guard.Against(Status != TripStatus.Dispatched, "Pre-trip inspection can only be submitted for dispatched trips.");
        Guard.Against(PreInspection is not null, "A pre-trip inspection has already been submitted for this trip.");

        PreInspection = TripPreInspection.Create(
            Id, odometerStart, fuelLevel,
            weatherType, temperature, roadConditions, visibility, roadAdvisories, weatherPulledAt,
            items);
    }

    public void SubmitPostReport(
        int odometerEnd,
        decimal? fuelAddedLitres,
        decimal? fuelCostDollars,
        bool hasIncident,
        IncidentType? incidentType,
        string? incidentDescription,
        string? additionalNotes,
        bool isReadyToInvoice,
        bool exteriorNoNewDamage = false,
        bool interiorCleanedAndChecked = false,
        bool passengersDisembarkedSafely = false,
        bool allCargoDeliveredAndAccounted = false,
        bool vehicleSecuredAndPluggedIn = false,
        bool keysReturnedAndSecured = false)
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
            isReadyToInvoice,
            exteriorNoNewDamage,
            interiorCleanedAndChecked,
            passengersDisembarkedSafely,
            allCargoDeliveredAndAccounted,
            vehicleSecuredAndPluggedIn,
            keysReturnedAndSecured);

        Status = TripStatus.Completed;
        RaiseDomainEvent(new TripCompletedEvent(Id));
    }

    public void SoftDelete()
    {
        Guard.Against(Status == TripStatus.Completed, "Completed trips cannot be deleted.");
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        Guard.Against(!IsDeleted, "Trip is not deleted.");
        IsDeleted = false;
        DeletedAt = null;
    }
}
