using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record GetTripByIdQuery(Guid Id) : IQuery<TripDetailResult>;

public sealed record TripDetailResult(
    Guid Id,
    Guid ClientId,
    Guid VehicleId,
    Guid? DriverId,
    string? PurchaseOrderNumber,
    string? VehicleType,
    DateTime ScheduledAt,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<TripStopResult> Stops,
    TripPreInspectionResult? PreInspection,
    TripPostReportResult? PostReport);

public sealed record TripStopResult(
    Guid Id,
    int SequenceOrder,
    string LocationName,
    string? Address);

public sealed record TripPreInspectionResult(
    Guid Id,
    int OdometerStart,
    DateTime SubmittedAt,
    IReadOnlyList<TripInspectionItemResult> Items);

public sealed record TripInspectionItemResult(
    Guid Id,
    string ItemName,
    bool Passed,
    string? Notes);

public sealed record TripPostReportResult(
    Guid Id,
    int OdometerStart,
    int OdometerEnd,
    int DistanceKm,
    decimal? FuelAddedLitres,
    decimal? FuelCostDollars,
    bool HasIncident,
    string? IncidentType,
    string? IncidentDescription,
    string? AdditionalNotes,
    DateTime SubmittedAt,
    bool IsReadyToInvoice);
