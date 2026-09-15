using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

/// <summary>
/// User.Create validates its role against Roles.Internal. The role travels verbatim into the
/// access token's "role" claim and is then compared by RequireRole, which matches ordinally and
/// case-sensitively — so anything not caught here authenticates fine and then 403s every request,
/// which is a far worse failure than being rejected at creation.
/// </summary>
public class UserRoleTests
{
    private static Result<User> Create(string role) =>
        User.Create(SeedTenant.Id, "someone@northernlink.ca", "hashed:pw", role);

    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Accountant)]
    [InlineData(Roles.BoardMember)]
    [InlineData(Roles.Driver)]
    public void Every_internal_role_is_accepted(string role)
    {
        var result = Create(role);

        Assert.True(result.IsSuccess);
        Assert.Equal(role, result.Value.Role);
    }

    [Fact]
    public void Roles_Internal_and_the_accepted_set_stay_in_step()
    {
        // Guards against a constant being added to Roles without a matching InlineData above.
        Assert.All(Roles.Internal, role => Assert.True(Create(role).IsSuccess, role));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bookkeeper")]
    [InlineData("SuperUser")]
    public void Unknown_role_is_rejected(string role)
    {
        var result = Create(role);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidRole, result.Error);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("OWNER")]
    [InlineData("accountant")]
    public void Role_matching_is_case_sensitive(string role)
    {
        // RequireRole compares ordinally, so a case variant would pass creation and then be
        // denied by every policy — fail here instead, where the message can say why.
        var result = Create(role);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidRole, result.Error);
    }

    [Fact]
    public void Legacy_Admin_cannot_be_used_to_create_a_new_user()
    {
        // Roles.LegacyAdmin exists only so the AdminOnly policy keeps honouring access tokens
        // minted before RenameAdminRoleToOwner ran. It is not a role anyone can be given.
        var result = Create(Roles.LegacyAdmin);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidRole, result.Error);
        Assert.DoesNotContain(Roles.LegacyAdmin, Roles.Internal);
    }

    [Fact]
    public void Role_is_stored_trimmed()
    {
        var result = Create($"  {Roles.Accountant}  ");

        Assert.True(result.IsSuccess);
        Assert.Equal(Roles.Accountant, result.Value.Role);
    }

    [Fact]
    public void BudgetAccess_is_a_subset_of_the_internal_roles()
    {
        // A typo here would produce a policy that no user can ever satisfy: the whole
        // /api/budgeting group carries BudgetAccess, so every budgeting request would 403 for
        // everyone, with nothing pointing at the role list as the cause.
        Assert.All(Roles.BudgetAccess, role => Assert.Contains(role, Roles.Internal));
        Assert.Equal([Roles.Owner, Roles.Accountant], Roles.BudgetAccess);
    }

    [Fact]
    public void DispatchAccess_is_a_subset_of_the_internal_roles()
    {
        // The counterpart to BudgetAccess above, and the wider blast radius of the two: this
        // list gates all of /api/notifications (passenger emails, template authoring) and all
        // of /api/booking. A role literal here that is not in Roles.Internal is a role no user
        // can ever hold, so the policy would deny everyone, everywhere, silently.
        Assert.All(Roles.DispatchAccess, role => Assert.Contains(role, Roles.Internal));
        Assert.Equal([Roles.Owner, Roles.Dispatcher, Roles.Supervisor], Roles.DispatchAccess);
    }

    [Fact]
    public void The_capability_role_sets_are_disjoint_where_they_are_meant_to_be()
    {
        // Accountant has budget access and no dispatch access; Dispatcher and Supervisor the
        // reverse. Owner is the only role in both, and that overlap is the deliberate one —
        // pinning it means widening either set trips a test rather than passing unnoticed.
        var both = Roles.BudgetAccess.Intersect(Roles.DispatchAccess, StringComparer.Ordinal).ToList();

        Assert.Equal([Roles.Owner], both);
        Assert.DoesNotContain(Roles.Driver, Roles.BudgetAccess);
        Assert.DoesNotContain(Roles.Driver, Roles.DispatchAccess);
        Assert.DoesNotContain(Roles.LegacyAdmin, Roles.BudgetAccess);
        Assert.DoesNotContain(Roles.LegacyAdmin, Roles.DispatchAccess);
    }
}
