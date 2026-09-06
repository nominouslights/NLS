using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Application.Settings.GetSettings;

public sealed class GetBookingSettingsQueryHandler(
    IBookingPolicyRepository policies,
    ICorridorSettingsRepository corridorSettings,
    ICorridorLookupRepository corridors)
    : IQueryHandler<GetBookingSettingsQuery, BookingSettingsResponse>
{
    public async Task<Result<BookingSettingsResponse>> Handle(
        GetBookingSettingsQuery query, CancellationToken cancellationToken)
    {
        var policy = await policies.GetAsync(cancellationToken);
        var settings = await corridorSettings.GetAllAsync(cancellationToken);
        var lookups = await corridors.GetAllAsync(cancellationToken);
        var namesById = lookups.ToDictionary(c => c.CorridorId, c => c.Name);

        var policyResponse = policy is null
            ? new BookingPolicyResponse(
                BookingPolicy.DefaultCancellationWindowHours,
                BookingPolicy.DefaultEarlyCancellationPenaltyCad,
                BookingPolicy.DefaultBookingCutoffHours,
                BookingPolicy.DefaultSeatHoldMinutes,
                BookingPolicy.DefaultPassengerMinimumValue,
                BookingPolicy.DefaultSeatCapacityValue,
                IsPersisted: false)
            : new BookingPolicyResponse(
                policy.CancellationWindowHours,
                policy.EarlyCancellationPenaltyCad,
                policy.BookingCutoffHours,
                policy.SeatHoldMinutes,
                policy.DefaultPassengerMinimum,
                policy.DefaultSeatCapacity,
                IsPersisted: true);

        var corridorResponses = settings
            .Select(s => new CorridorSettingsResponse(
                s.CorridorId,
                namesById.GetValueOrDefault(s.CorridorId, s.CorridorId.ToString()),
                s.PassengerMinimum,
                s.SeatCapacity))
            .OrderBy(s => s.CorridorName)
            .ToList();

        return Result.Success(new BookingSettingsResponse(policyResponse, corridorResponses));
    }
}
