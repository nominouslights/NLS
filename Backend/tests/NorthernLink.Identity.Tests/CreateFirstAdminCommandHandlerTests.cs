using NorthernLink.Identity.Application.Auth.Setup;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

public class CreateFirstAdminCommandHandlerTests
{
    private static CreateFirstAdminCommandHandler Handler(
        InMemoryUserRepository userRepository,
        InMemoryRefreshTokenRepository? refreshTokenRepository = null) =>
        new(userRepository,
            refreshTokenRepository ?? new InMemoryRefreshTokenRepository(),
            new FakePasswordHasher(),
            new FakeAccessTokenIssuer());

    [Fact]
    public async Task Empty_users_table_creates_the_first_admin_and_signs_them_in()
    {
        var userRepository = new InMemoryUserRepository();
        var refreshTokenRepository = new InMemoryRefreshTokenRepository();
        var handler = Handler(userRepository, refreshTokenRepository);

        var result = await handler.Handle(
            new CreateFirstAdminCommand("owner@northernlink.ca", "correct horse battery"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(userRepository.Users);
        Assert.Equal("Admin", user.Role);
        Assert.Equal(SeedTenant.Id, user.TenantId);
        Assert.Equal("owner@northernlink.ca", user.Email);

        // Signed straight in: an access token plus a stored refresh token whose hash
        // matches the raw one returned.
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        var refreshToken = Assert.Single(refreshTokenRepository.Tokens);
        Assert.Equal(user.Id, refreshToken.UserId);
        Assert.Equal($"hashed:{result.Value.RefreshToken}", refreshToken.TokenHash);
    }

    [Fact]
    public async Task Existing_user_closes_the_gate_with_a_conflict()
    {
        var userRepository = new InMemoryUserRepository();
        userRepository.Add(TestUsers.Create(email: "first@northernlink.ca"));
        var handler = Handler(userRepository);

        var result = await handler.Handle(
            new CreateFirstAdminCommand("second@northernlink.ca", "some password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.SetupAlreadyCompleted, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Single(userRepository.Users);
    }

    [Fact]
    public async Task Blank_password_is_a_validation_failure()
    {
        var userRepository = new InMemoryUserRepository();
        var handler = Handler(userRepository);

        var result = await handler.Handle(
            new CreateFirstAdminCommand("owner@northernlink.ca", "   "),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidPassword, result.Error);
        Assert.Empty(userRepository.Users);
    }

    [Fact]
    public async Task Bad_email_is_a_validation_failure()
    {
        var userRepository = new InMemoryUserRepository();
        var handler = Handler(userRepository);

        var result = await handler.Handle(
            new CreateFirstAdminCommand("not-an-email", "some password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidEmail, result.Error);
        Assert.Empty(userRepository.Users);
    }
}
