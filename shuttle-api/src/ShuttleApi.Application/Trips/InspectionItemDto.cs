namespace ShuttleApi.Application.Trips;

public sealed record InspectionItemDto(string ItemName, bool Passed, string? Notes);
