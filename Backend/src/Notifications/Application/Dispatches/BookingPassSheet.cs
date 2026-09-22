namespace NorthernLink.Notifications.Application.Dispatches;

/// <summary>
/// The data for one community booking's passes email — a flat, presentation-ready snapshot
/// composed entirely by the dispatcher's booking detail screen. All fields are
/// already-formatted strings (<paramref name="ServiceDate"/> like "Tuesday, September 15,
/// 2026", <paramref name="PaymentStatus"/> like "Paid"); the composer does no domain lookups
/// of its own — Notifications holds no booking data by design, same as
/// <see cref="ClientAccrualsReport"/>. One <see cref="BookingPassTraveller"/> per seat.
/// </summary>
public sealed record BookingPassSheet(
    string Reference,
    string ServiceDate,
    string CorridorName,
    string Pickup,
    string Dropoff,
    string CustomerName,
    string PaymentMethod,
    string PaymentStatus,
    string? Notes,
    IReadOnlyList<BookingPassTraveller> Travellers);

/// <summary>One traveller's pass: name, optional phone, and the seat label ("1 of 3").</summary>
public sealed record BookingPassTraveller(
    string Name,
    string? Phone,
    string Seat);
