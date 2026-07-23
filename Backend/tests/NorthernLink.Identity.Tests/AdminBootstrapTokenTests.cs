using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

public class AdminBootstrapTokenTests
{
    private static AdminBootstrapToken Issue(TimeSpan? untilExpiry = null) =>
        AdminBootstrapToken.Issue(
            SeedTenant.Id,
            "hashed:opaque-1",
            DateTimeOffset.UtcNow.Add(untilExpiry ?? TimeSpan.FromMinutes(15)));

    [Fact]
    public void Consume_succeeds_exactly_once()
    {
        var token = Issue();

        var first = token.Consume();

        Assert.True(first.IsSuccess);
        Assert.True(token.IsConsumed);
        Assert.NotNull(token.ConsumedAtUtc);

        var second = token.Consume();

        Assert.True(second.IsFailure);
        Assert.Equal(UserErrors.BootstrapTokenAlreadyConsumed, second.Error);
    }

    [Fact]
    public void Expired_token_cannot_be_consumed()
    {
        var token = Issue(untilExpiry: TimeSpan.FromMinutes(-1));

        var result = token.Consume();

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidBootstrapToken, result.Error);
        Assert.False(token.IsConsumed);
        Assert.Null(token.ConsumedAtUtc);
    }

    [Fact]
    public void IsExpired_flips_at_the_expiry_instant()
    {
        // ExpiresAtUtc <= now counts as expired: a moment already passed is expired,
        // a moment still ahead is not.
        Assert.True(Issue(untilExpiry: TimeSpan.FromMilliseconds(-1)).IsExpired);
        Assert.False(Issue(untilExpiry: TimeSpan.FromMinutes(5)).IsExpired);
    }
}
