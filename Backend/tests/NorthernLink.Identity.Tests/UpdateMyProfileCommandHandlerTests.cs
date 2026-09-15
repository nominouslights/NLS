using NorthernLink.Identity.Application.Profile.UpdateProfile;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

/// <summary>
/// The self-service profile write. The handler itself is thin by design — every rule lives in
/// <c>User.UpdateProfile</c> — so what these tests pin is the handler's own responsibilities:
/// look the caller up within their tenant, never save a rejected change, and hand back what was
/// actually stored.
/// </summary>
public class UpdateMyProfileCommandHandlerTests
{
    private readonly InMemoryUserRepository _repository = new();
    private readonly UpdateMyProfileCommandHandler _handler;

    public UpdateMyProfileCommandHandlerTests()
    {
        _handler = new UpdateMyProfileCommandHandler(_repository);
    }

    private User Existing(string email = "planner@northernlink.ca", string role = Roles.Accountant)
    {
        var user = TestUsers.Create(email, role);
        _repository.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task A_valid_update_stores_the_profile_and_saves_once()
    {
        var user = Existing();

        var result = await _handler.Handle(
            new UpdateMyProfileCommand(user.Id, "Léa Fontaine", "Financial Planner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Léa Fontaine", user.FullName);
        Assert.Equal("Financial Planner", user.JobTitle);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_response_carries_what_was_stored_not_what_was_sent()
    {
        // The console renders straight from this payload, so it has to be the normalized value —
        // otherwise the form keeps showing the user's stray whitespace back to them.
        var user = Existing();

        var result = await _handler.Handle(
            new UpdateMyProfileCommand(user.Id, "  Léa Fontaine  ", "   "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("planner@northernlink.ca", result.Value.Email);
        Assert.Equal(Roles.Accountant, result.Value.Role);
        Assert.Equal("Léa Fontaine", result.Value.FullName);
        Assert.Null(result.Value.JobTitle);
    }

    [Fact]
    public async Task An_unknown_user_is_a_not_found_and_saves_nothing()
    {
        var result = await _handler.Handle(
            new UpdateMyProfileCommand(Guid.NewGuid(), "Léa Fontaine", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.NotFound, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_user_in_another_tenant_is_invisible_even_with_the_right_id()
    {
        // The repository read is tenant-scoped rather than riding the app.is_system escape
        // hatch, so knowing an id from another tenant buys nothing.
        var user = Existing();
        _repository.TenantId = Guid.NewGuid();

        var result = await _handler.Handle(
            new UpdateMyProfileCommand(user.Id, "Léa Fontaine", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.NotFound, result.Error);
        Assert.Null(user.FullName);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_rejected_change_is_not_saved()
    {
        var user = Existing();

        var result = await _handler.Handle(
            new UpdateMyProfileCommand(
                user.Id, new string('a', User.ProfileFieldMaxLength + 1), null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.FullNameTooLong, result.Error);
        Assert.Null(user.FullName);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }
}
