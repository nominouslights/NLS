using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record InspectionItemDto(string ItemName, InspectionCategory Category, bool Passed, string? Notes);
