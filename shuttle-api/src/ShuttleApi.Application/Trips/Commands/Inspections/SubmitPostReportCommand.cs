using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record SubmitPostReportCommand(
    Guid TripId,
    int OdometerEnd,
    decimal? FuelAddedLitres,
    decimal? FuelCostDollars,
    bool HasIncident,
    IncidentType? IncidentType,
    string? IncidentDescription,
    string? AdditionalNotes,
    bool IsReadyToInvoice,
    bool ExteriorNoNewDamage = false,
    bool InteriorCleanedAndChecked = false,
    bool PassengersDisembarkedSafely = false,
    bool AllCargoDeliveredAndAccounted = false,
    bool VehicleSecuredAndPluggedIn = false,
    bool KeysReturnedAndSecured = false) : ICommand;
