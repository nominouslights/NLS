namespace ShuttleApi.Application.Trips;

public sealed record StopDto(int SequenceOrder, string LocationName, string? Address);
