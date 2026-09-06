using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Bookings.Confirm;

public sealed record ConfirmBookingCommand(Guid TenantId, Guid BookingId) : ICommand;
