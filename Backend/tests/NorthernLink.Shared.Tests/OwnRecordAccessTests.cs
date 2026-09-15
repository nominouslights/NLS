using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Shared.Tests;

/// <summary>
/// The platform-wide caller-owns-this-row decision, tested away from any module. Drivers is its
/// first consumer (<c>IDriverSelfAccess</c>), but the failure modes it guards are generic and the
/// next module to adopt it inherits these guarantees rather than re-deriving them:
/// the override short-circuit must not run the lookup, a missing principal must deny, and role
/// matching must stay ordinal.
/// </summary>
public class OwnRecordAccessTests
{
    private static readonly Guid MyRecord = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TheirRecord = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid MyUserId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    /// <summary>A lookup that reports whether it was called, so the short-circuit is observable.</summary>
    private sealed class Lookup(Guid? result)
    {
        public int Calls { get; private set; }

        public Task<Guid?> Resolve(Guid userId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task The_owner_of_the_record_is_allowed()
    {
        var lookup = new Lookup(MyRecord);
        var actor = new FakeActor(MyUserId, Roles.Driver);

        Assert.True(await actor.AllowsAsync(MyRecord, Roles.DispatchAccess, lookup.Resolve));
    }

    [Fact]
    public async Task Somebody_elses_record_is_denied()
    {
        var lookup = new Lookup(MyRecord);
        var actor = new FakeActor(MyUserId, Roles.Driver);

        Assert.False(await actor.AllowsAsync(TheirRecord, Roles.DispatchAccess, lookup.Resolve));
    }

    [Fact]
    public async Task A_caller_who_owns_no_record_is_denied()
    {
        var lookup = new Lookup(null);
        var actor = new FakeActor(MyUserId, Roles.Driver);

        Assert.False(await actor.AllowsAsync(MyRecord, Roles.DispatchAccess, lookup.Resolve));
    }

    [Fact]
    public async Task An_override_role_is_allowed_without_consulting_the_lookup()
    {
        // Both halves matter: dispatch may act on anyone, and it must not pay for a query it
        // cannot fail. A lookup that ran anyway would also mask a bug where it throws.
        var lookup = new Lookup(null);
        var actor = new FakeActor(Guid.NewGuid(), Roles.Dispatcher);

        Assert.True(await actor.AllowsAsync(TheirRecord, Roles.DispatchAccess, lookup.Resolve));
        Assert.Equal(0, lookup.Calls);
    }

    [Fact]
    public async Task An_actor_with_no_user_id_is_denied_without_consulting_the_lookup()
    {
        // Background work (projections, outbox) has no principal. "No actor" must never read as
        // "owns everything", and Guid.Empty must not be invented as a stand-in user id.
        var lookup = new Lookup(MyRecord);
        var actor = new FakeActor(null, Roles.Driver);

        Assert.False(await actor.AllowsAsync(MyRecord, Roles.DispatchAccess, lookup.Resolve));
        Assert.Equal(0, lookup.Calls);
    }

    [Fact]
    public void Role_matching_is_ordinal_and_case_sensitive()
    {
        // RequireRole compares ordinally and User.Create refuses a miscased role outright, so a
        // case-insensitive match here would grant override to a string the platform rejects.
        Assert.True(new FakeActor(null, Roles.Dispatcher).HoldsAnyRole(Roles.DispatchAccess));
        Assert.False(new FakeActor(null, "dispatcher").HoldsAnyRole(Roles.DispatchAccess));
        Assert.False(new FakeActor(null, "DISPATCHER").HoldsAnyRole(Roles.DispatchAccess));
    }

    [Fact]
    public void An_actor_holding_no_roles_matches_nothing() =>
        Assert.False(new FakeActor(MyUserId).HoldsAnyRole(Roles.DispatchAccess));

    private sealed class FakeActor(Guid? userId, params string[] roles) : ICurrentActor
    {
        public Guid? UserId => userId;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => roles;
    }
}
