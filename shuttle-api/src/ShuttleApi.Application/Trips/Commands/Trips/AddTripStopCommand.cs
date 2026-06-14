using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record AddTripStopCommand(
    Guid TripId,
    int InsertAtSequenceOrder,
    string LocationName,
    string? Address) : ICommand<TripStopResult>;
