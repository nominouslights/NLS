using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Application.Settings.UpdatePolicy;

public sealed class UpdateBookingPolicyCommandHandler(IBookingPolicyRepository repository)
    : ICommandHandler<UpdateBookingPolicyCommand>
{
    public async Task<Result> Handle(UpdateBookingPolicyCommand command, CancellationToken cancellationToken)
    {
        var policy = await repository.GetAsync(cancellationToken);
        if (policy is null)
        {
            policy = BookingPolicy.CreateDefault(command.TenantId);
            repository.Add(policy);
        }

        var result = policy.Update(
            command.CancellationWindowHours,
            command.EarlyCancellationPenaltyCad,
            command.BookingCutoffHours,
            command.SeatHoldMinutes,
            command.DefaultPassengerMinimum,
            command.DefaultSeatCapacity);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
