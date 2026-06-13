using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Passengers.Queries;

public sealed record SearchPassengerProfilesQuery(Guid ClientId, string Query)
    : IQuery<IReadOnlyList<PassengerProfileResult>>;

public sealed record PassengerProfileResult(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    DateTime LastBookedAt);
