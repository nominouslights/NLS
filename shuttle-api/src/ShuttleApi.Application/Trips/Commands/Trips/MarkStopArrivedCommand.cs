using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record MarkStopArrivedCommand(Guid TripId, Guid StopId) : ICommand;
