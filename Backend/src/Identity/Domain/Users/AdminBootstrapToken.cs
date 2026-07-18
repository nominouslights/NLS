using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Domain.Users;

/// <summary>
/// A one-time-use token that lets whoever holds it mint a new Admin <see cref="User"/> via
/// the anonymous bootstrap endpoint, without needing an existing session. Only
/// <see cref="TokenHash"/> is persisted — the raw value is returned to the (already
/// authenticated) admin who generated it exactly once. "Active" = the latest row for a
/// tenant with <see cref="ConsumedAtUtc"/> still null.
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
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public bool IsConsumed => ConsumedAtUtc is not null;

    public static AdminBootstrapToken Issue(Guid tenantId, string tokenHash) =>
        new()
        {
            TenantId = tenantId,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

    public Result Consume()
    {
        if (IsConsumed)
        {
            return Result.Failure(UserErrors.BootstrapTokenAlreadyConsumed);
        }

        ConsumedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
