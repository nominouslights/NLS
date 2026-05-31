using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Community.Commands;

public sealed record BookSeatCommand(
    DateOnly Date,
    string Direction,
    string TripType,
    string Destination,
    string FullName,
    string Phone,
    string Email) : ICommand<BookSeatResult>;

public sealed record BookSeatResult(
    Guid PassengerId,
    string BookingReference,
    DateTime CutoffDeadline,
    decimal Fare,
    string Route,
    string FullName,
    string? Phone,
    string? Email,
    string Direction,
    string TripType,
    DateOnly DepartureDate,
    string Status,
    DateTime BookedAt);
