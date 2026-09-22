using NorthernLink.Booking.Application.Customers;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings;

/// <summary>
/// Public contract for a booking. <see cref="Reference"/> is the immutable customer-facing
/// identifier (<c>NL-XXXXXX</c>) printed on passes. Status, PaymentMethod, and PaymentStatus
/// travel as enum names ("Unconfirmed"/"Confirmed"/"Cancelled", "Square"/"ETransfer"/"Cash",
/// "Unpaid"/"Paid" — the gateway serializes enums as strings). <see cref="HoldExpired"/>
/// is derived at read time: true only for an Unconfirmed booking whose hold has lapsed
/// (it stays listed but no longer reserves seats — show it gold "hold expired").
/// </summary>
public sealed record BookingResponse(
    Guid Id,
    string Reference,
    Guid CustomerId,
    string CustomerName,
    Guid CorridorId,
    string CorridorName,
    DateOnly ServiceDate,
    BookingStatus Status,
    BookingLocationResponse Pickup,
    BookingLocationResponse Dropoff,
    IReadOnlyList<BookingPassengerResponse> Passengers,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DateTimeOffset HoldExpiresAtUtc,
    bool HoldExpired,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// The single-booking detail: the booking plus the customer who requested it (the full
/// roster row — phone and email, not just the name snapshot). <see cref="Customer"/> is null
/// only if the roster row has since vanished; the booking itself is never returned partially.
/// </summary>
public sealed record BookingDetailResponse(
    BookingResponse Booking,
    CustomerResponse? Customer);

/// <summary>A pickup/drop-off location: an optional stop reference + name snapshot, and/or address text.</summary>
public sealed record BookingLocationResponse(
    Guid? StopId,
    string? StopName,
    string? AddressDetail);

/// <summary>One seat occupant.</summary>
public sealed record BookingPassengerResponse(
    Guid Id,
    string Name,
    string? Phone,
    bool IsBillingCustomer);

/// <summary>Raw location input on create/update commands — validated into a BookingLocation.</summary>
public sealed record BookingLocationInput(
    Guid? StopId,
    string? StopName,
    string? AddressDetail);

/// <summary>Raw passenger input on create/update commands — validated by the aggregate.</summary>
public sealed record BookingPassengerInput(
    string? Name,
    string? Phone,
    bool IsBillingCustomer);
