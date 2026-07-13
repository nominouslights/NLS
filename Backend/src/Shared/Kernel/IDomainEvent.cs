namespace NorthernLink.Shared.Kernel;

/// <summary>
/// An event raised inside a domain library's model. Handled in-process, within the library.
/// (Cross-domain communication uses integration events over the bus instead — see
/// IIntegrationEventBus in NorthernLink.Shared.Events.)
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
