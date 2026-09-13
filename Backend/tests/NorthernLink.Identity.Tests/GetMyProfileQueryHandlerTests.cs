using NorthernLink.Identity.Application.Profile.GetProfile;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

public class GetMyProfileQueryHandlerTests
{
    private readonly InMemoryUserRepository _repository = new();
    private readonly GetMyProfileQueryHandler _handler;

    public GetMyProfileQueryHandlerTests()
    {
        _handler = new GetMyProfileQueryHandler(_repository);
    }

    [Fact]
    public async Task Returns_the_stored_account_including_the_fields_that_are_not_editable()
    {
        var user = TestUsers.Create("planner@northernlink.ca", Roles.Accountant);
        user.UpdateProfile("Léa Fontaine", "Financial Planner");
        _repository.Users.Add(user);

        var result = await _handler.Handle(new GetMyProfileQuery(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("planner@northernlink.ca", result.Value.Email);
        Assert.Equal(Roles.Accountant, result.Value.Role);
        Assert.Equal("Léa Fontaine", result.Value.FullName);
        Assert.Equal("Financial Planner", result.Value.JobTitle);
    }

    [Fact]
    public async Task An_account_with_no_profile_yet_reads_back_as_null_not_blank()
    {
        var user = TestUsers.Create();
        _repository.Users.Add(user);

        var result = await _handler.Handle(new GetMyProfileQuery(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.FullName);
        Assert.Null(result.Value.JobTitle);
    }

    [Fact]
    public async Task An_unknown_user_is_a_not_found()
    {
        // A signed token whose account has since been removed: authenticated, but there is
        // nothing to return.
        var result = await _handler.Handle(
            new GetMyProfileQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task A_user_in_another_tenant_is_invisible()
    {
        var user = TestUsers.Create();
        _repository.Users.Add(user);
        _repository.TenantId = Guid.NewGuid();

        var result = await _handler.Handle(new GetMyProfileQuery(user.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.NotFound, result.Error);
    }
}
