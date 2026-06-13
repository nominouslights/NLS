using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record SubmitPreInspectionCommand(
    Guid TripId,
    int OdometerStart,
    FuelLevel FuelLevel,
    string? WeatherType,
    string? Temperature,
    string? RoadConditions,
    string? Visibility,
    string? RoadAdvisories,
    DateTime? WeatherPulledAt,
    IReadOnlyList<InspectionItemDto> Items) : ICommand;
