using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Identity.Application.Auth.VerifyPassword;
using NorthernLink.Identity.Domain.Users;
using Xunit;

namespace NorthernLink.Identity.Tests;

/// <summary>
/// Step-up re-authentication: the same hasher as login, no tokens minted, and the account under
/// test is always the command's own <c>UserId</c> (the endpoint takes that from the <c>sub</c>
/// claim, never from the body).
/// </summary>
public class VerifyPasswordCommandHandlerTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();

    private VerifyPasswordCommandHandler Handler() =>
        new(_users, _hasher, NullLogger<VerifyPasswordCommandHandler>.Instance);

    /// <summary>Adds a user whose stored hash is the fake hasher's hash of <c>pw-{email}</c>.</summary>
    private User AddUser(string email = "owner@northernlink.ca")
    {
        var user = TestUsers.Create(email);
        _users.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task The_correct_password_verifies()
    {
        var user = AddUser();

        var result = await Handler().Handle(
            new VerifyPasswordCommand(user.Id, "pw-owner@northernlink.ca"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_wrong_password_is_rejected_as_invalid_credentials()
    {
        var user = AddUser();

        var result = await Handler().Handle(
            new VerifyPasswordCommand(user.Id, "not-the-password"), CancellationToken.None);

        Assert.Equal(UserErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task A_blank_password_is_a_validation_failure_not_a_verification_attempt()
    {
        var user = AddUser();

        var empty = await Handler().Handle(
            new VerifyPasswordCommand(user.Id, string.Empty), CancellationToken.None);
        var whitespace = await Handler().Handle(
            new VerifyPasswordCommand(user.Id, "   "), CancellationToken.None);

        Assert.Equal(UserErrors.InvalidPassword, empty.Error);
        Assert.Equal(UserErrors.InvalidPassword, whitespace.Error);
    }

    [Fact]
    public async Task An_unknown_subject_fails_exactly_like_a_wrong_password()
    {
        // A token whose user has since been deleted must not be distinguishable from a bad
        // password — same generic failure as the login path.
        AddUser();

        var result = await Handler().Handle(
            new VerifyPasswordCommand(Guid.NewGuid(), "pw-owner@northernlink.ca"), CancellationToken.None);

        Assert.Equal(UserErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Verification_mints_nothing_and_touches_no_persistence()
    {
        // Explicitly not a login: no refresh-token rotation, no save. If this ever starts
        // writing, the endpoint has quietly become a second login path.
        var user = AddUser();

        var result = await Handler().Handle(
            new VerifyPasswordCommand(user.Id, "pw-owner@northernlink.ca"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _users.SaveChangesCallCount);
    }
}
