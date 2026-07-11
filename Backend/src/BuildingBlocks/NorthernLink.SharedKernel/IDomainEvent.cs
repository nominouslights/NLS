namespace NorthernLink.SharedKernel;

/// <summary>
/// An event raised inside a module's domain model. Handled in-process, within the module.
/// (Cross-module communication uses integration events over the bus instead — see
/// IIntegrationEventBus in BuildingBlocks.Application.)
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
