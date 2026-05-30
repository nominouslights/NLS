using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Community.Queries;

public sealed record GetBookingByReferenceQuery(string Reference)
    : IQuery<BookingDetailResult>;

public sealed record BookingDetailResult(
    string BookingReference,
    string FullName,
    string? Phone,
    string? Email,
    string? Direction,
    string TripType,
    DateOnly DepartureDate,
    string Route,
    decimal Fare,
    string Status,
    DateTime? CutoffDeadline,
    DateTime BookedAt);
