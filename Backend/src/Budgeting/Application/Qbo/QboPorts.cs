namespace NorthernLink.Budgeting.Application.Qbo;

// The four ports the Intuit integration sits behind. Strings, Guids and the records in this
// folder only — no domain type crosses them — so a future Accounting module can lift
// Infrastructure/Qbo out unchanged.

/// <summary>Intuit's OAuth 2.0 endpoints. Every failure is a <see cref="QboAuthException"/>.</summary>
public interface IQboAuthClient
{
    /// <summary>
    /// The URL the browser is sent to. Requests the <c>com.intuit.quickbooks.accounting</c> scope
    /// and nothing else.
    /// </summary>
    string BuildAuthorizeUrl(string state);

    /// <summary>Trades the callback's authorization code for a token pair.</summary>
    Task<QboTokenGrant> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Trades a refresh token for a new pair. Intuit may rotate the refresh token: the returned
    /// one replaces the old one and must be persisted before the access token is used.
    /// </summary>
    Task<QboTokenGrant> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Revokes a token (revoking the refresh token ends the whole grant).</summary>
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}

/// <summary>The company's name and country, from <c>CompanyInfo</c>.</summary>
public sealed record QboCompanyInfo(string CompanyName, string? Country);

/// <summary>
/// The QuickBooks Accounting API, read-only. This slice needs only what connecting checks; the
/// expense import adds query and change-data-capture calls. Every failure is a
/// <see cref="QboApiException"/>.
/// </summary>
public interface IQboAccountingClient
{
    /// <summary>"Sandbox" or "Production" — which Intuit API host this client talks to.</summary>
    string Environment { get; }

    Task<QboCompanyInfo> GetCompanyInfoAsync(string realmId, string accessToken, CancellationToken cancellationToken);

    /// <summary>The company's home currency code (e.g. "CAD") from <c>Preferences</c>, or null when absent.</summary>
    Task<string?> GetHomeCurrencyAsync(string realmId, string accessToken, CancellationToken cancellationToken);
}

/// <summary>
/// Encrypts tokens for the vault. The (tenant, realm) pair is bound into each ciphertext as
/// associated data, so a ciphertext copied onto another tenant's row fails to decrypt rather than
/// handing that tenant someone else's QuickBooks access.
/// </summary>
public interface IQboTokenProtector
{
    /// <summary>Identifies the key <see cref="Protect"/> uses now (stored beside the ciphertext).</summary>
    string CurrentKeyId { get; }

    string Protect(string plaintext, Guid tenantId, string realmId);

    /// <summary>
    /// Decrypts with whichever configured key produced the value. Throws
    /// <see cref="System.Security.Cryptography.CryptographicException"/> on tampering, a
    /// (tenant, realm) mismatch, a malformed value, or a key that is no longer configured.
    /// </summary>
    string Unprotect(string protectedValue, Guid tenantId, string realmId);
}

/// <summary>
/// The tenant's QuickBooks tokens, encrypted at rest in <c>budgeting.qbo_token_vault</c>. Keyed
/// by tenant: one company per tenant, so one row.
/// </summary>
public interface IQboTokenStore
{
    /// <summary>Stores (or replaces) the tenant's tokens after an OAuth flow.</summary>
    Task StoreGrantAsync(Guid tenantId, string realmId, QboTokenGrant grant, CancellationToken cancellationToken);

    /// <summary>
    /// A currently valid access token, refreshing first when it is near expiry. The vault row is
    /// locked for the refresh and a rotated refresh token is committed before the access token is
    /// returned. Throws <see cref="QboReconnectRequiredException"/> when there is no usable token,
    /// having already marked the live connection NeedsReconnect.
    /// </summary>
    Task<string> GetAccessTokenAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The stored refresh token for a revoke, or null when none is stored or it no longer decrypts.</summary>
    Task<string?> ReadRefreshTokenAsync(Guid tenantId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken);
}
