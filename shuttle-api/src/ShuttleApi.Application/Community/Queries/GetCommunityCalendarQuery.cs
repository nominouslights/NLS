using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Community.Queries;

public sealed record GetCommunityCalendarQuery(bool IsAdmin = false)
    : IQuery<IReadOnlyList<CalendarDayResult>>;

public sealed record CalendarDayResult(
    DateOnly Date,
    string DayOfWeek,
    string Status,           // "Go" | "Building" | "Open" | "Unavailable"
    bool IsZone2,
    int ConfirmedCount,
    int TentativeCount,
    int AvailableSeats,
    Guid? TripId,
    bool IsBlocked,
    string? BlockReason);    // Only set for admin requests
