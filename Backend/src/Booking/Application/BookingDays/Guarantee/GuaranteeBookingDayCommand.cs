using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.BookingDays.Guarantee;

/// <summary>
/// Gift-a-Seat (dispatcher-side): guarantees the day's passenger minimum, re-confirming a
/// Reverted day. Idempotent — repeating it on an already-guaranteed day succeeds unchanged.
/// </summary>
public sealed record GuaranteeBookingDayCommand(Guid TenantId, Guid BookingDayId) : ICommand;
