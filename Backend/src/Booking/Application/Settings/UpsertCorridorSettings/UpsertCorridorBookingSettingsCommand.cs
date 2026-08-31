using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Settings.UpsertCorridorSettings;

/// <summary>Creates or updates one corridor's override row. Null fields clear the override.</summary>
public sealed record UpsertCorridorBookingSettingsCommand(
    Guid TenantId,
    Guid CorridorId,
    int? PassengerMinimum,
    int? SeatCapacity) : ICommand;
