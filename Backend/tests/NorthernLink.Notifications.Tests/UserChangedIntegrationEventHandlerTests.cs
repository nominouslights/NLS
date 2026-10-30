using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Notifications.Application.Integration;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The user replica's consumer. Outbox delivery is at-least-once, so "the same event twice" is
/// the normal case here, not an edge case — these tests pin that the handler converges rather
/// than duplicating, and that it mirrors Budgeting's replica row for row.
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
        Guid userId,
        string email = "officer@northernlink.ca",
        string role = Roles.ComplianceOfficer,
        Guid? tenantId = null,
        string? fullName = null) =>
        new(userId, tenantId ?? TestNotifications.TenantId, email, role, fullName, JobTitle: null);

    [Fact]
    public async Task A_first_event_inserts_the_replica_row_as_active()
    {
        var userId = Guid.NewGuid();

        await _handler.Handle(Event(userId, fullName: "Léa Fontaine"), CancellationToken.None);

        var stored = Assert.Single(_repository.Users);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal(TestNotifications.TenantId, stored.TenantId);
        Assert.Equal("officer@northernlink.ca", stored.Email);
        Assert.Equal("Léa Fontaine", stored.FullName);
        Assert.Equal(Roles.ComplianceOfficer, stored.Role);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Replaying_the_same_event_leaves_exactly_one_row()
    {
        var integrationEvent = Event(Guid.NewGuid());

        await _handler.Handle(integrationEvent, CancellationToken.None);
        await _handler.Handle(integrationEvent, CancellationToken.None);

        Assert.Single(_repository.Users);
    }

    [Fact]
    public async Task A_later_event_for_the_same_user_updates_in_place()
    {
        var userId = Guid.NewGuid();
        await _handler.Handle(Event(userId), CancellationToken.None);

        await _handler.Handle(Event(userId, "renamed@northernlink.ca", Roles.Owner), CancellationToken.None);

        var stored = Assert.Single(_repository.Users);
        Assert.Equal("renamed@northernlink.ca", stored.Email);
        Assert.Equal(Roles.Owner, stored.Role);
    }

    [Fact]
    public async Task A_later_event_with_no_name_clears_the_stored_one()
    {
        // The regression that catches an omitted assignment in UpsertAsync: a name would insert
        // once and then never be correctable, silently.
        var userId = Guid.NewGuid();
        await _handler.Handle(Event(userId, fullName: "Léa Fontaine"), CancellationToken.None);

        await _handler.Handle(Event(userId, fullName: null), CancellationToken.None);

        Assert.Null(Assert.Single(_repository.Users).FullName);
    }

    [Fact]
    public async Task A_role_change_moves_the_user_out_of_an_alerts_audience()
    {
        // The reason the replica exists: audience selection reads the replicated role, so a
        // demotion from ComplianceOfficer must stop the alerts without any other change.
        var userId = Guid.NewGuid();
        await _handler.Handle(Event(userId, role: Roles.ComplianceOfficer), CancellationToken.None);
        Assert.Single(await _repository.ListActiveByRolesAsync(TestNotifications.TenantId, Roles.ComplianceAccess));

        await _handler.Handle(Event(userId, role: Roles.Dispatcher), CancellationToken.None);

        Assert.Empty(await _repository.ListActiveByRolesAsync(TestNotifications.TenantId, Roles.ComplianceAccess));
    }

    [Fact]
    public async Task The_same_user_id_in_two_tenants_stays_two_rows()
    {
        var userId = Guid.NewGuid();
        var otherTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await _handler.Handle(Event(userId, "mine@northernlink.ca"), CancellationToken.None);
        await _handler.Handle(Event(userId, "theirs@example.org", Roles.Owner, otherTenantId), CancellationToken.None);

        Assert.Equal(2, _repository.Users.Count);
        Assert.Equal("mine@northernlink.ca", Assert.Single(_repository.Users, u => u.TenantId == TestNotifications.TenantId).Email);
        Assert.Equal("theirs@example.org", Assert.Single(_repository.Users, u => u.TenantId == otherTenantId).Email);
    }
}
