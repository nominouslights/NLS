using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.CreateDeadheadReturn;

/// <summary>
/// Mints the new leg's trip number from the same per-tenant sequence as ad-hoc creation,
/// then delegates eligibility and the reversed-leg shape to
/// <see cref="Trip.CreateDeadheadReturn"/>. A refused source burns a number, which is
/// acceptable — trip numbers are unique, not gapless (see <see cref="ITripNumberGenerator"/>).
/// One save commits the new leg and the source's pairing atomically.
/// </summary>
public sealed class CreateDeadheadReturnCommandHandler(
    ITripRepository tripRepository,
    ITripNumberGenerator tripNumberGenerator)
    : ICommandHandler<CreateDeadheadReturnCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDeadheadReturnCommand command, CancellationToken cancellationToken)
    {
        var source = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (source is null)
        {
            return Result.Failure<Guid>(TripErrors.NotFound);
        }

        var tripNumber = await tripNumberGenerator.NextAsync(source.TenantId, cancellationToken);

        var returnTrip = source.CreateDeadheadReturn(tripNumber);
        if (returnTrip.IsFailure)
        {
            return Result.Failure<Guid>(returnTrip.Error);
        }

        tripRepository.Add(returnTrip.Value);
        await tripRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(returnTrip.Value.Id);
    }
}
