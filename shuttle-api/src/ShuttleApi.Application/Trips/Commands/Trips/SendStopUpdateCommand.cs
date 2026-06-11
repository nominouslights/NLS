using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record SendStopUpdateCommand(
    Guid TripId,
    Guid? StopId,
    string? Status) : ICommand;
