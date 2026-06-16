using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record MarkStopDepartedCommand(Guid TripId, Guid StopId) : ICommand;
