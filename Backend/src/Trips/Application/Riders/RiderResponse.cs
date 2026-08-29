using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Riders;

/// <summary>
/// Public contract for a rider-directory entry (the Riders screen and the manifest rider
/// picker). <see cref="ServiceType"/> travels as the enum name ("ContractCrew", …).
/// <see cref="NextExpectedTravelDate"/> is computed server-side as
/// <c>LastTripDate + RotationDays</c> and is null unless both are set.
/// </summary>
public sealed record RiderResponse(
    Guid Id,
    string Name,
    TripServiceType ServiceType,
    string? Contact,
    int? RotationDays,
    DateOnly? LastTripDate,
    string? LastTripNumber,
    DateOnly? NextExpectedTravelDate,
    int TripCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
