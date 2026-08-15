using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Budgeting.Application.Integration;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The user replica's consumer. Outbox delivery is at-least-once and subscribing replays a
/// routing key's whole history, so "the same event twice" is the normal case here, not an edge
/// case — these tests pin that the handler converges rather than duplicating.
/// </summary>
public class UserChangedIntegrationEventHandlerTests
{
    private readonly InMemoryUserLookupRepository _repository = new();
    private readonly UserChangedIntegrationEventHandler _handler;

    public UserChangedIntegrationEventHandlerTests()
    {
        _handler = new UserChangedIntegrationEventHandler(
            _repository, NullLogger<UserChangedIntegrationEventHandler>.Instance);
    }

    private static UserChangedIntegrationEvent Event(
        Guid userId, string email = "planner@northernlink.ca", string role = Roles.Accountant) =>
        new(userId, TestBudgeting.TenantId, email, role);

    [Fact]
    public async Task A_first_event_inserts_the_replica_row()
    {
        var userId = Guid.NewGuid();

        await _handler.Handle(Event(userId), CancellationToken.None);

        var stored = Assert.Single(_repository.Users);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal(TestBudgeting.TenantId, stored.TenantId);
        Assert.Equal("planner@northernlink.ca", stored.Email);
        Assert.Equal(Roles.Accountant, stored.Role);
    }

    [Fact]
    public async Task Replaying_the_same_event_leaves_exactly_one_row()
    {
        var userId = Guid.NewGuid();
        var integrationEvent = Event(userId);

        await _handler.Handle(integrationEvent, CancellationToken.None);
        await _handler.Handle(integrationEvent, CancellationToken.None);

        Assert.Single(_repository.Users);
    }

    [Fact]
    public async Task A_later_event_for_the_same_user_updates_in_place()
    {
        // Identity.User is create-only today, so this path is unreachable until it grows a
        // rename or role change. The test exists so the replica is already correct when it does —
        // the alternative is discovering the handler duplicates rows on the day that ships.
        var userId = Guid.NewGuid();
        await _handler.Handle(Event(userId), CancellationToken.None);

        await _handler.Handle(Event(userId, "renamed@northernlink.ca", Roles.Owner), CancellationToken.None);

        var stored = Assert.Single(_repository.Users);
        Assert.Equal("renamed@northernlink.ca", stored.Email);
        Assert.Equal(Roles.Owner, stored.Role);
    }

    [Fact]
    public async Task Two_different_users_produce_two_rows()
    {
        await _handler.Handle(Event(Guid.NewGuid(), "a@northernlink.ca"), CancellationToken.None);
        await _handler.Handle(Event(Guid.NewGuid(), "b@northernlink.ca"), CancellationToken.None);

        Assert.Equal(2, _repository.Users.Count);
    }
}
