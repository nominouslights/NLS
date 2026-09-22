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
        Guid userId,
        string email = "planner@northernlink.ca",
        string role = Roles.Accountant,
        Guid? tenantId = null,
        string? fullName = null,
        string? jobTitle = null) =>
        new(userId, tenantId ?? TestBudgeting.TenantId, email, role, fullName, jobTitle);

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
        // Live since profiles shipped: editing a profile republishes the whole user snapshot,
        // so this is now the ordinary case rather than a path kept warm for later.
        var userId = Guid.NewGuid();
        await _handler.Handle(Event(userId), CancellationToken.None);

        await _handler.Handle(Event(userId, "renamed@northernlink.ca", Roles.Owner), CancellationToken.None);

        var stored = Assert.Single(_repository.Users);
        Assert.Equal("renamed@northernlink.ca", stored.Email);
        Assert.Equal(Roles.Owner, stored.Role);
    }

    [Fact]
    public async Task A_first_event_stores_the_name_when_the_user_has_one()
    {
        var userId = Guid.NewGuid();

        await _handler.Handle(Event(userId, fullName: "Léa Fontaine"), CancellationToken.None);

        Assert.Equal("Léa Fontaine", Assert.Single(_repository.Users).FullName);
    }

    [Fact]
    public async Task An_account_with_no_profile_replicates_as_null_not_blank()
    {
        // The whole display chain — owner picker, the three governance rows — falls back to the
        // email on null. An empty string here would render as a blank name instead.
        var userId = Guid.NewGuid();

        await _handler.Handle(Event(userId), CancellationToken.None);

        Assert.Null(Assert.Single(_repository.Users).FullName);
    }

    [Fact]
    public async Task A_later_event_updates_the_name_in_place()
    {
        var userId = Guid.NewGuid();
        await _handler.Handle(Event(userId, fullName: "Lea Fontaine"), CancellationToken.None);

        await _handler.Handle(Event(userId, fullName: "Léa Fontaine"), CancellationToken.None);

        Assert.Equal("Léa Fontaine", Assert.Single(_repository.Users).FullName);
    }

    [Fact]
    public async Task A_later_event_with_no_name_clears_the_stored_one()
    {
        // The regression that catches an omitted assignment in UpsertAsync. Without it a name
        // inserts once and can then never be corrected or removed — silently, with nothing
        // logged, because the insert path would still look perfectly correct.
        var userId = Guid.NewGuid();
        await _handler.Handle(Event(userId, fullName: "Léa Fontaine"), CancellationToken.None);

        await _handler.Handle(Event(userId, fullName: null), CancellationToken.None);

        Assert.Null(Assert.Single(_repository.Users).FullName);
    }

    [Fact]
    public async Task Two_different_users_produce_two_rows()
    {
        await _handler.Handle(Event(Guid.NewGuid(), "a@northernlink.ca"), CancellationToken.None);
        await _handler.Handle(Event(Guid.NewGuid(), "b@northernlink.ca"), CancellationToken.None);

        Assert.Equal(2, _repository.Users.Count);
    }

    [Fact]
    public async Task The_same_user_id_in_two_tenants_stays_two_rows()
    {
        // The one place in the codebase where tenant scoping is NOT delegated to the EF query
        // filter: UserLookupRepository.UpsertAsync runs IgnoreQueryFilters() (the handler's
        // DbContext captured a null tenant before the ambient push) and therefore carries its
        // own `u.UserId == user.UserId && u.TenantId == user.TenantId` predicate. Drop the
        // tenant half and one user id arriving for two tenants collapses to a single row —
        // one tenant's replica silently overwritten by another's. Nothing else pins that
        // predicate, so this test is the guard.
        var userId = Guid.NewGuid();
        var otherTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await _handler.Handle(
            Event(userId, "shared@northernlink.ca", Roles.Accountant), CancellationToken.None);
        await _handler.Handle(
            Event(userId, "other-tenant@northernlink.ca", Roles.Owner, otherTenantId),
            CancellationToken.None);

        Assert.Equal(2, _repository.Users.Count);
        var mine = Assert.Single(_repository.Users, u => u.TenantId == TestBudgeting.TenantId);
        Assert.Equal("shared@northernlink.ca", mine.Email);
        Assert.Equal(Roles.Accountant, mine.Role);
        var theirs = Assert.Single(_repository.Users, u => u.TenantId == otherTenantId);
        Assert.Equal("other-tenant@northernlink.ca", theirs.Email);
        Assert.Equal(Roles.Owner, theirs.Role);
    }

    [Fact]
    public async Task A_replica_row_is_only_visible_to_its_own_tenant()
    {
        // The read half of the same rule: GetAsync/ListAsync go through the EF query filter
        // (BudgetingDbContext: u.TenantId == TenantId), so another tenant's row must not be
        // reachable even though it shares the user id.
        var userId = Guid.NewGuid();
        var otherTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await _handler.Handle(
            Event(userId, "other-tenant@northernlink.ca", Roles.Owner, otherTenantId),
            CancellationToken.None);

        Assert.Null(await _repository.GetAsync(userId, CancellationToken.None));
        Assert.Empty(await _repository.ListAsync(CancellationToken.None));
    }
}
