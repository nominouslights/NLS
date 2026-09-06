using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Bookings.Cancel;

public sealed record CancelBookingCommand(Guid TenantId, Guid BookingId) : ICommand;
