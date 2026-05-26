using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record SubmitPreInspectionCommand(
    Guid TripId,
    int OdometerStart,
    IReadOnlyList<InspectionItemDto> Items) : ICommand;
