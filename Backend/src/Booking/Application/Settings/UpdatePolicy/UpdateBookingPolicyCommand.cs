using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Settings.UpdatePolicy;

/// <summary>Get-or-create write: materializes the policy row with these values if none exists.</summary>
public sealed record UpdateBookingPolicyCommand(
    Guid TenantId,
    int CancellationWindowHours,
    decimal EarlyCancellationPenaltyCad,
    int BookingCutoffHours,
    int SeatHoldMinutes,
    int DefaultPassengerMinimum,
    int DefaultSeatCapacity) : ICommand;
