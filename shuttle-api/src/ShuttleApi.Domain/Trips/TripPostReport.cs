using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripPostReport : Entity<Guid>
{
    public Guid TripId { get; private set; }
    public int OdometerStart { get; private set; }
    public int OdometerEnd { get; private set; }
    public int DistanceKm => OdometerEnd - OdometerStart;
    public decimal? FuelAddedLitres { get; private set; }
    public decimal? FuelCostDollars { get; private set; }
    public bool HasIncident { get; private set; }
    public IncidentType? IncidentType { get; private set; }
    public string? IncidentDescription { get; private set; }
    public string? AdditionalNotes { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public bool IsReadyToInvoice { get; private set; }

    // Post-trip vehicle checklist (Section 9 of physical manifest)
    public bool ExteriorNoNewDamage { get; private set; }
    public bool InteriorCleanedAndChecked { get; private set; }
    public bool PassengersDisembarkedSafely { get; private set; }
    public bool AllCargoDeliveredAndAccounted { get; private set; }
    public bool VehicleSecuredAndPluggedIn { get; private set; }
    public bool KeysReturnedAndSecured { get; private set; }

    private TripPostReport() { }

    public static TripPostReport Create(
        Guid tripId,
        int odometerStart,
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
        return new TripPostReport
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            OdometerStart = odometerStart,
            OdometerEnd = odometerEnd,
            FuelAddedLitres = fuelAddedLitres,
            FuelCostDollars = fuelCostDollars,
            HasIncident = hasIncident,
            IncidentType = incidentType,
            IncidentDescription = incidentDescription,
            AdditionalNotes = additionalNotes,
            SubmittedAt = DateTime.UtcNow,
            IsReadyToInvoice = isReadyToInvoice,
            ExteriorNoNewDamage = exteriorNoNewDamage,
            InteriorCleanedAndChecked = interiorCleanedAndChecked,
            PassengersDisembarkedSafely = passengersDisembarkedSafely,
            AllCargoDeliveredAndAccounted = allCargoDeliveredAndAccounted,
            VehicleSecuredAndPluggedIn = vehicleSecuredAndPluggedIn,
            KeysReturnedAndSecured = keysReturnedAndSecured
        };
    }
}
