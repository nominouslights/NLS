using System.Security.Cryptography;
using System.Text;

namespace NorthernLink.Budgeting.Application.Qbo;

/// <summary>
/// One pending OAuth round trip, in <c>budgeting.qbo_oauth_states</c>. A plain keyed row, not an
/// aggregate and not audited (the <see cref="Integration.UserLookup"/> precedent). Only the
/// SHA-256 of the state is stored, so reading the table never yields a usable state value. Single
/// use, valid for <see cref="Lifetime"/>, and bound to the tenant and user that started it.
/// </summary>
public sealed class QboOAuthState
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Lowercase hex SHA-256 of the state value sent to Intuit.</summary>
    public string StateHash { get; set; } = null!;

    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }

    /// <summary>32 random bytes, base64url — unguessable, and safe in a query string unescaped.</summary>
    public static string GenerateValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Lowercase hex SHA-256 of a state value as sent.</summary>
    public static string Hash(string state) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
}
