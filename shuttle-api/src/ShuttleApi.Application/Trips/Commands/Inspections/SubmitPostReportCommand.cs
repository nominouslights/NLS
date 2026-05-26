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
    bool IsReadyToInvoice) : ICommand;
