using NorthernLink.Identity.Application.Auth.BootstrapAdmin;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

public class BootstrapAdminCommandHandlerTests
{
    private const string RawToken = "opaque-1";

    private readonly InMemoryAdminBootstrapTokenRepository _tokenRepository = new();
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly BootstrapAdminCommandHandler _handler;

    public BootstrapAdminCommandHandlerTests()
    {
        _handler = new BootstrapAdminCommandHandler(
            _tokenRepository, _userRepository, new FakePasswordHasher(), new FakeAccessTokenIssuer());
    }

    private AdminBootstrapToken IssueActiveToken(TimeSpan? untilExpiry = null, string role = Roles.Owner)
    {
        var token = AdminBootstrapToken.Issue(
            SeedTenant.Id,
            $"hashed:{RawToken}",
            DateTimeOffset.UtcNow.Add(untilExpiry ?? TimeSpan.FromMinutes(15)),
            role);
        _tokenRepository.Add(token);
        return token;
    }

    private static BootstrapAdminCommand Command(
        string token = RawToken,
        string email = "new-admin@northernlink.ca",
        string password = "some password") => new(token, email, password);

    [Fact]
    public async Task Unknown_token_is_unauthorized()
    {
        var result = await _handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidBootstrapToken, result.Error);
        Assert.Empty(_userRepository.Users);
    }

    [Fact]
    public async Task Wrong_token_value_is_unauthorized_and_leaves_the_token_active()
    {
        var token = IssueActiveToken();

        var result = await _handler.Handle(Command(token: "not-the-token"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidBootstrapToken, result.Error);
        Assert.False(token.IsConsumed);
        Assert.Empty(_userRepository.Users);
    }

    [Fact]
    public async Task Expired_token_is_unauthorized_and_not_consumed()
    {
        var token = IssueActiveToken(untilExpiry: TimeSpan.FromMinutes(-1));

        var result = await _handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidBootstrapToken, result.Error);
        Assert.False(token.IsConsumed);
        Assert.Empty(_userRepository.Users);
    }

    [Fact]
    public async Task Blank_password_is_a_validation_failure()
    {
        IssueActiveToken();

        var result = await _handler.Handle(Command(password: "   "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidPassword, result.Error);
    }

    [Fact]
    public async Task Duplicate_email_is_a_conflict_and_the_token_stays_redeemable()
    {
        var token = IssueActiveToken();
        _userRepository.Add(TestUsers.Create(email: "taken@northernlink.ca"));

        var result = await _handler.Handle(Command(email: "taken@northernlink.ca"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.DuplicateEmail, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.False(token.IsConsumed);
        Assert.Equal(0, _userRepository.SaveChangesCallCount);
        Assert.Single(_userRepository.Users);
    }

    [Fact]
    public async Task Duplicate_email_that_races_past_the_precheck_is_still_a_conflict()
    {
        IssueActiveToken();
        _userRepository.FailNextTryAddNewUser = true;

        var result = await _handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.DuplicateEmail, result.Error);
        // The failed commit persisted nothing: no user row, no save.
        Assert.Empty(_userRepository.Users);
        Assert.Equal(0, _userRepository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Valid_token_creates_the_admin_and_consumes_the_token_in_one_save()
    {
        var token = IssueActiveToken();

        var result = await _handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(_userRepository.Users);
        Assert.Equal(result.Value, user.Id);
        Assert.Equal(Roles.Owner, user.Role);
        Assert.Equal(SeedTenant.Id, user.TenantId);
        Assert.True(token.IsConsumed);
        Assert.Equal(1, _userRepository.SaveChangesCallCount);

        // Consumed means gone: the same token cannot be redeemed twice.
        var secondAttempt = await _handler.Handle(Command(email: "another@northernlink.ca"), CancellationToken.None);
        Assert.True(secondAttempt.IsFailure);
        Assert.Equal(UserErrors.InvalidBootstrapToken, secondAttempt.Error);
    }

    // The whole point of putting the role on the invite: this is how a non-Owner account comes
    // to exist at all, and therefore how the Budgeting console's rejection path can be exercised
    // against a real token rather than a hand-built one.
    [Theory]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Accountant)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Driver)]
    public async Task New_user_takes_the_role_recorded_on_the_invite(string role)
    {
        IssueActiveToken(role: role);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(_userRepository.Users);
        Assert.Equal(role, user.Role);
    }
}
