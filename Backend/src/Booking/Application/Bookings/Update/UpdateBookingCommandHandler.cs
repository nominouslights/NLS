using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.Update;

public sealed class UpdateBookingCommandHandler(IBookingRepository repository)
    : ICommandHandler<UpdateBookingCommand>
{
    public async Task<Result> Handle(UpdateBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        var pickup = BookingLocation.Create(
            command.Pickup.StopId, command.Pickup.StopName, command.Pickup.AddressDetail);
        if (pickup.IsFailure)
        {
            return Result.Failure(pickup.Error);
        }

        var dropoff = BookingLocation.Create(
            command.Dropoff.StopId, command.Dropoff.StopName, command.Dropoff.AddressDetail);
        if (dropoff.IsFailure)
        {
            return Result.Failure(dropoff.Error);
        }

        var result = booking.Update(
            pickup.Value,
            dropoff.Value,
            [.. command.Passengers.Select(p => new BookingPassengerDetails(p.Name, p.Phone, p.IsBillingCustomer))],
            command.PaymentMethod,
            command.PaymentStatus,
            command.Notes);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
