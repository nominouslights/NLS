using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Domain.Users;

/// <summary>
/// A server-side record of an issued opaque refresh token. Only <see cref="TokenHash"/>
/// (SHA-256 of the raw value) is ever persisted — the raw token is returned to the caller
/// once and never stored. Not <see cref="ITenantScoped"/> directly: it belongs to a
/// <see cref="User"/>, which is itself tenant-scoped, so scoping flows through
/// <see cref="UserId"/> rather than being duplicated here.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
        // EF Core materialization only.
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc) =>
        new()
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

    public void Revoke() => RevokedAtUtc ??= DateTimeOffset.UtcNow;
}
