using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Application.Settings.UpsertCorridorSettings;

public sealed class UpsertCorridorBookingSettingsCommandHandler(
    ICorridorSettingsRepository repository,
    ICorridorLookupRepository corridors)
    : ICommandHandler<UpsertCorridorBookingSettingsCommand>
{
    public async Task<Result> Handle(
        UpsertCorridorBookingSettingsCommand command, CancellationToken cancellationToken)
    {
        var corridor = await corridors.GetAsync(command.CorridorId, cancellationToken);
        if (corridor is null)
        {
            return Result.Failure(BookingErrors.CorridorNotFound);
        }

        var existing = await repository.GetByCorridorAsync(command.CorridorId, cancellationToken);
        if (existing is null)
        {
            var created = CorridorBookingSettings.Create(
                command.TenantId, command.CorridorId, command.PassengerMinimum, command.SeatCapacity);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            repository.Add(created.Value);
        }
        else
        {
            var updated = existing.Update(command.PassengerMinimum, command.SeatCapacity);
            if (updated.IsFailure)
            {
                return updated;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
