using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Bookings.GetById;

/// <summary>One booking's detail (booking + its customer) for the booking detail screen.</summary>
public sealed record GetBookingByIdQuery(Guid TenantId, Guid BookingId) : IQuery<BookingDetailResponse>;
