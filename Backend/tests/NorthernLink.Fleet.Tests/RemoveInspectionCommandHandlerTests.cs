using NorthernLink.Fleet.Application.Inspections.Remove;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The remove handler: loads tenant-filtered (a missing/cross-tenant id yields
/// <see cref="InspectionErrors.NotFound"/>), raises <see cref="VehicleInspectionRemovedDomainEvent"/>
/// on the aggregate so the mapper can carry the removal across the module boundary, then hard-deletes.
/// </summary>
public class RemoveInspectionCommandHandlerTests
{
    [Fact]
    public async Task An_unknown_id_returns_not_found()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new RemoveInspectionCommandHandler(repository);

        var result = await handler.Handle(new RemoveInspectionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.NotFound, result.Error);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Removes_the_inspection_and_raises_the_removed_event()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var stored = TestInspections.PostTrip();
        repository.Add(stored);

        var handler = new RemoveInspectionCommandHandler(repository);
        var result = await handler.Handle(new RemoveInspectionCommand(stored.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Inspections); // hard-deleted
        Assert.Equal(1, repository.SaveChangesCallCount);

        // The removal event was raised on the aggregate before the delete, carrying the identity
        // the mapper needs (the row is gone by the time any downstream reaction runs).
        var removed = Assert.Single(stored.DomainEvents.OfType<VehicleInspectionRemovedDomainEvent>());
        Assert.Equal(stored.Id, removed.InspectionId);
        Assert.Equal(stored.TenantId, removed.TenantId);
    }
}
