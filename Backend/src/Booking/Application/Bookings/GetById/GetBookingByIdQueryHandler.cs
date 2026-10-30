using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.GetById;

/// <summary>
/// Handles <see cref="GetBookingByIdQuery"/>. The read service is tenant-filtered, so an
/// unknown id and another tenant's booking both surface as the same
/// <see cref="BookingErrors.NotFound"/> — never a hint that the row exists elsewhere.
/// </summary>
public sealed class GetBookingByIdQueryHandler(IBookingReadService readService)
    : IQueryHandler<GetBookingByIdQuery, BookingDetailResponse>
{
    public async Task<Result<BookingDetailResponse>> Handle(
        GetBookingByIdQuery query, CancellationToken cancellationToken)
    {
        var detail = await readService.GetByIdAsync(query.BookingId, cancellationToken);
        return detail is null
            ? Result.Failure<BookingDetailResponse>(BookingErrors.NotFound)
            : Result.Success(detail);
    }
}
