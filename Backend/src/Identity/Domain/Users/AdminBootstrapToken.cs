using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Domain.Users;

/// <summary>
/// A one-time-use token that lets whoever holds it mint a new Admin <see cref="User"/> via
/// the anonymous bootstrap endpoint, without needing an existing session. Only
/// <see cref="TokenHash"/> is persisted — the raw value is returned to the (already
/// authenticated) admin who generated it exactly once. "Active" = the latest row for a
/// tenant with <see cref="ConsumedAtUtc"/> still null; an active token can still be
/// past <see cref="ExpiresAtUtc"/>, in which case <see cref="Consume"/> refuses it.
/// </summary>
public sealed class AdminBootstrapToken : Entity, ITenantScoped
{
    private AdminBootstrapToken()
    {
        // EF Core materialization only.
        TokenHash = null!;
    }

    public Guid TenantId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public bool IsConsumed => ConsumedAtUtc is not null;

    public bool IsExpired => ExpiresAtUtc <= DateTimeOffset.UtcNow;

    public static AdminBootstrapToken Issue(Guid tenantId, string tokenHash, DateTimeOffset expiresAtUtc) =>
        new()
        {
            TenantId = tenantId,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
        };

    public Result Consume()
    {
        if (IsConsumed)
        {
            return Result.Failure(UserErrors.BootstrapTokenAlreadyConsumed);
        }

        // Same deliberately generic error as an unknown token — the consumer is anonymous,
        // so an expired token must not be distinguishable from one that never existed.
        if (IsExpired)
        {
            return Result.Failure(UserErrors.InvalidBootstrapToken);
        }

        ConsumedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
