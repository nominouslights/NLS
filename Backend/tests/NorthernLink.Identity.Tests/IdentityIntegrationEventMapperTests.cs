using NorthernLink.Identity.Application.Auth;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Identity.Domain.Users.Events;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

/// <summary>
/// Identity's first integration-event mapper. What it publishes is a public contract other
/// modules build replicas from, so the mapping is pinned here rather than left to inspection:
/// a field silently dropped in translation shows up as a permanently-blank column in another
/// module, with no error on either side.
/// </summary>
public class IdentityIntegrationEventMapperTests
{
    private readonly IdentityIntegrationEventMapper _mapper = new();

    [Fact]
    public void User_creation_maps_to_a_user_changed_event_carrying_every_field()
    {
        var user = TestUsers.Create("planner@northernlink.ca", Roles.Accountant);
        var domainEvent = Assert.Single(user.DomainEvents.OfType<UserCreatedDomainEvent>());

        var mapped = _mapper.Map(domainEvent, user);

        var integrationEvent = Assert.IsType<UserChangedIntegrationEvent>(mapped);
        Assert.Equal(user.Id, integrationEvent.UserId);
        Assert.Equal(SeedTenant.Id, integrationEvent.TenantId);
        Assert.Equal("planner@northernlink.ca", integrationEvent.Email);
        Assert.Equal(Roles.Accountant, integrationEvent.Role);
    }

    [Fact]
    public void The_email_travels_normalized_exactly_as_the_aggregate_stored_it()
    {
        // User.Create lower-cases and trims. The replica keys its display on this value, so the
        // two must never disagree — mapping user.Email rather than the event's copy is what
        // guarantees it.
        var user = TestUsers.Create("  Planner@NorthernLink.CA  ");
        var domainEvent = Assert.Single(user.DomainEvents.OfType<UserCreatedDomainEvent>());

        var integrationEvent = Assert.IsType<UserChangedIntegrationEvent>(_mapper.Map(domainEvent, user));

        Assert.Equal("planner@northernlink.ca", integrationEvent.Email);
        Assert.Equal(user.Email, integrationEvent.Email);
    }

    [Fact]
    public void An_unmapped_domain_event_publishes_nothing()
    {
        // The switch is deliberately exhaustive-by-omission: anything without an arm stays
        // internal rather than leaking onto a bus no module asked for.
        var user = TestUsers.Create();

        Assert.Null(_mapper.Map(new UnmappedDomainEvent(), user));
    }

    [Fact]
    public void A_user_created_event_paired_with_a_foreign_aggregate_publishes_nothing()
    {
        // Guards the `when aggregate is User` arm rather than trusting the event type alone.
        var user = TestUsers.Create();
        var domainEvent = Assert.Single(user.DomainEvents.OfType<UserCreatedDomainEvent>());

        Assert.Null(_mapper.Map(domainEvent, new ForeignAggregate()));
    }

    private sealed record UnmappedDomainEvent : IDomainEvent
    {
        public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }

    private sealed class ForeignAggregate : AggregateRoot;
}
