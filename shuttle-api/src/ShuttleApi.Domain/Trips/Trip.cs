using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips.Events;

namespace ShuttleApi.Domain.Trips;

public sealed class Trip : AggregateRoot<Guid>
{
    private readonly List<TripStop> _stops = [];

    public Guid ClientId { get; private set; }
    public Guid? DriverId { get; private set; }
    public string? PurchaseOrderNumber { get; private set; }
    public string? VehicleType { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public TripStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<TripStop> Stops => _stops.AsReadOnly();
    public TripPreInspection? PreInspection { get; private set; }
    public TripPostReport? PostReport { get; private set; }

    private Trip() { }

    public static Trip Create(
        Guid clientId,
        string? purchaseOrderNumber,
        string? vehicleType,
        DateTime scheduledAt,
        string? notes,
        IEnumerable<(int SequenceOrder, string LocationName, string? Address)> stops)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            PurchaseOrderNumber = purchaseOrderNumber,
            VehicleType = vehicleType,
            ScheduledAt = scheduledAt,
            Status = TripStatus.Scheduled,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (seq, loc, addr) in stops)
            trip._stops.Add(TripStop.Create(trip.Id, seq, loc, addr));

        trip.RaiseDomainEvent(new TripCreatedEvent(trip.Id));

        return trip;
    }

    public void Update(
        string? purchaseOrderNumber,
        string? vehicleType,
        DateTime scheduledAt,
        string? notes,
        IEnumerable<(int SequenceOrder, string LocationName, string? Address)> stops)
    {
        Guard.Against(Status != TripStatus.Scheduled, "Only scheduled trips can be updated.");

        PurchaseOrderNumber = purchaseOrderNumber;
        VehicleType = vehicleType;
        ScheduledAt = scheduledAt;
        Notes = notes;

        _stops.Clear();
        foreach (var (seq, loc, addr) in stops)
            _stops.Add(TripStop.Create(Id, seq, loc, addr));
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
