using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Bookings;

namespace NorthernLink.Booking.Application.Calendar.GetDay;

/// <summary>
/// The side-panel detail for one corridor + date: the derived seat math plus every booking
/// (cancelled included — revert-not-delete keeps them listed). Valid for any date, booked
/// or not — an unbooked date returns zeros with a null BookingDayId.
/// </summary>
public sealed record GetBookingDayDetailQuery(
    Guid TenantId,
    Guid CorridorId,
    DateOnly Date) : IQuery<BookingDayDetailResponse>;

/// <summary>
/// One day's panel: identity (BookingDayId null until the day materializes; TripId always
/// null this batch — the panel shows "no trip yet"), the active overrides, the derived
/// seat numbers, and the full booking list.
/// </summary>
public sealed record BookingDayDetailResponse(
    DateOnly Date,
    Guid CorridorId,
    string CorridorName,
    Guid? BookingDayId,
    Guid? TripId,
    int? PassengerMinimumOverride,
    int? SeatCapacityOverride,
    int Sold,
    int Pending,
    int Capacity,
    int Remaining,
    int PassengerMinimum,
    int NeededToConfirm,
    IReadOnlyList<BookingResponse> Bookings);
