using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Settings.SetDayOverrides;

/// <summary>
/// Sets one day's minimum/capacity overrides. Targets an existing BookingDay by id (the
/// calendar/day queries return it once the day has materialized). Null fields clear the
/// override back to corridor/policy resolution.
/// </summary>
public sealed record SetBookingDayOverridesCommand(
    Guid TenantId,
    Guid BookingDayId,
    int? PassengerMinimum,
    int? SeatCapacity) : ICommand;
