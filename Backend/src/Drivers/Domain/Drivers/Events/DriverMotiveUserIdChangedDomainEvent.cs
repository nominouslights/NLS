using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers.Events;

/// <summary>
/// Raised when a driver's Motive user id is set, changed or cleared — i.e. when whose Motive
/// duty-status logs land on this driver's ledger changes. Its own event so the journal shows it
/// distinctly from a roster edit. Internal to the module: <c>DriversIntegrationEventMapper</c>
/// maps it to null (no other module needs the Motive id).
/// </summary>
public sealed record DriverMotiveUserIdChangedDomainEvent(
    Guid DriverId,
    string? PreviousMotiveUserId,
    string? MotiveUserId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
