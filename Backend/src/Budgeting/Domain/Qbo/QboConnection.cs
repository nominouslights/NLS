using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Qbo.Events;

namespace NorthernLink.Budgeting.Domain.Qbo;

/// <summary>
/// A tenant's read-only link to one QuickBooks Online company (an Intuit "realm"). Budgeting owns
/// it because Budgeting is what reads QuickBooks: expenses come in, nothing goes out.
/// <para>
/// <b>No token ever lives on this aggregate, and none may be added.</b>
/// <c>ModuleDbContext.AppendAuditEntries</c> serializes every saved aggregate in full into
/// <c>aggregate_snapshots</c> and every domain event into <c>event_journal</c>, so a token here
/// would be copied, unencrypted, into an append-only audit table on every save. The access and
/// refresh tokens live encrypted in <c>budgeting.qbo_token_vault</c>, a plain table outside the
/// audit pipeline.
/// </para>
/// <para>
/// <b>Invariants.</b> At most one live (not <see cref="QboConnectionStatus.Disconnected"/>)
/// connection per tenant, backed by a partial unique index. One row per (tenant, realm): a
/// reconnect of the same company revives that row rather than adding another. Connecting is
/// refused when the company's home currency is not CAD, and — for now — when this tenant has ever
/// been connected to a different company (<see cref="EnsureSameCompany"/>), because imported
/// expense lines are keyed by realm and switching companies under them would orphan them.
/// </para>
/// </summary>
public sealed class QboConnection : AggregateRoot, ITenantScoped
{
    public const int RealmIdMaxLength = 32;
    public const int CompanyNameMaxLength = 256;
    public const int ErrorCodeMaxLength = 64;

    /// <summary>The only home currency a connection accepts. Budgets on this platform are CAD.</summary>
    public const string RequiredHomeCurrency = "CAD";

    /// <summary>How far a refresh must move the expiry before it is worth an audit row.</summary>
    public static readonly TimeSpan RefreshExpiryNoticeThreshold = TimeSpan.FromDays(1);

    private QboConnection()
    {
        // EF Core materialization only.
        RealmId = null!;
        CompanyName = null!;
    }

    public Guid TenantId { get; private set; }

    /// <summary>Intuit's company id (digits only), returned on the OAuth callback.</summary>
    public string RealmId { get; private set; }

    public string CompanyName { get; private set; }
    public QboEnvironment Environment { get; private set; }
    public QboConnectionStatus Status { get; private set; }

    /// <summary>The user (from the signed token) who last completed the OAuth flow.</summary>
    public Guid ConnectedBy { get; private set; }

    public DateTimeOffset ConnectedAtUtc { get; private set; }

    /// <summary>When the refresh token stops working unless it is used. Rolling: refreshes extend it.</summary>
    public DateTimeOffset RefreshTokenExpiresAtUtc { get; private set; }

    /// <summary>Set by the expense sync (a later slice). Null until the first successful sync.</summary>
    public DateTimeOffset? LastSuccessfulSyncAtUtc { get; private set; }

    /// <summary>The change-data-capture cursor the next incremental sync starts from (a later slice).</summary>
    public DateTimeOffset? LastSyncCursorUtc { get; private set; }

    /// <summary>Short machine code for why the connection needs reconnecting; null while healthy.</summary>
    public string? LastErrorCode { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Whether this connection still counts as the tenant's connection.</summary>
    public bool IsLive => Status != QboConnectionStatus.Disconnected;

    /// <summary>
    /// The different-company rule, checked against every connection the tenant has ever had —
    /// Disconnected ones included. Checked before any token is requested, since the realm id
    /// arrives on the callback.
    /// </summary>
    public static Result EnsureSameCompany(IEnumerable<QboConnection> existing, string realmId) =>
        existing.Any(c => !string.Equals(c.RealmId, realmId, StringComparison.Ordinal))
            ? Result.Failure(QboConnectionErrors.DifferentCompany)
            : Result.Success();

    /// <summary>Creates the tenant's first connection to <paramref name="realmId"/>, Active.</summary>
    public static Result<QboConnection> Connect(
        Guid tenantId,
        string realmId,
        string companyName,
        QboEnvironment environment,
        string? homeCurrency,
        Guid connectedBy,
        DateTimeOffset refreshTokenExpiresAtUtc,
        DateTimeOffset now)
    {
        var check = Validate(realmId, homeCurrency);
        if (check.IsFailure)
        {
            return Result.Failure<QboConnection>(check.Error);
        }

        var connection = new QboConnection
        {
            TenantId = tenantId,
            RealmId = realmId,
        };

        connection.Activate(companyName, environment, connectedBy, refreshTokenExpiresAtUtc, now, isReconnect: false);
        return Result.Success(connection);
    }

    /// <summary>
    /// Brings this connection back to Active after a fresh OAuth flow — from NeedsReconnect, from
    /// Disconnected, or over a still-Active connection (a person may simply connect again). The
    /// realm must be this connection's own.
    /// </summary>
    public Result Reconnect(
        string realmId,
        string companyName,
        QboEnvironment environment,
        string? homeCurrency,
        Guid connectedBy,
        DateTimeOffset refreshTokenExpiresAtUtc,
        DateTimeOffset now)
    {
        if (!string.Equals(realmId, RealmId, StringComparison.Ordinal))
        {
            return Result.Failure(QboConnectionErrors.DifferentCompany);
        }

        var check = Validate(realmId, homeCurrency);
        if (check.IsFailure)
        {
            return check;
        }

        Activate(companyName, environment, connectedBy, refreshTokenExpiresAtUtc, now, isReconnect: true);
        return Result.Success();
    }

    /// <summary>
    /// Flags an Active connection as needing a person to reconnect. Already flagged: nothing
    /// changes and no event is raised, so a worker retrying every few hours does not fill the
    /// audit trail. Disconnected: refused — there is nothing left to reconnect.
    /// </summary>
    public Result MarkNeedsReconnect(string errorCode, DateTimeOffset now)
    {
        switch (Status)
        {
            case QboConnectionStatus.Disconnected:
                return Result.Failure(QboConnectionErrors.AlreadyDisconnected);
            case QboConnectionStatus.NeedsReconnect:
                return Result.Success();
        }

        Status = QboConnectionStatus.NeedsReconnect;
        LastErrorCode = Truncate(errorCode, ErrorCodeMaxLength);
        UpdatedAtUtc = now;
        Raise(new QboConnectionNeedsReconnectDomainEvent(Id, TenantId, LastErrorCode));
        return Result.Success();
    }

    /// <summary>A person disconnects. The row stays as history; the vault row is the handler's to delete.</summary>
    public Result Disconnect(Guid? actorId, DateTimeOffset now)
    {
        if (Status == QboConnectionStatus.Disconnected)
        {
            return Result.Failure(QboConnectionErrors.AlreadyDisconnected);
        }

        Status = QboConnectionStatus.Disconnected;
        UpdatedAtUtc = now;
        Raise(new QboDisconnectedDomainEvent(Id, TenantId, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Records a refresh token's new expiry after a refresh. Returns whether anything changed:
    /// only a move of <see cref="RefreshExpiryNoticeThreshold"/> or more is recorded, so a token
    /// refreshed hourly costs at most about one audit row a day instead of twenty-four.
    /// </summary>
    public bool NoteRefreshTokenExpiry(DateTimeOffset refreshTokenExpiresAtUtc, DateTimeOffset now)
    {
        if (Status != QboConnectionStatus.Active
            || (refreshTokenExpiresAtUtc - RefreshTokenExpiresAtUtc).Duration() < RefreshExpiryNoticeThreshold)
        {
            return false;
        }

        RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
        UpdatedAtUtc = now;
        Raise(new QboRefreshTokenRenewedDomainEvent(Id, TenantId, refreshTokenExpiresAtUtc));
        return true;
    }

    /// <summary>Intuit realm ids are decimal digits; anything else never reaches a URL path.</summary>
    public static bool IsValidRealmId(string? realmId) =>
        !string.IsNullOrEmpty(realmId)
        && realmId.Length <= RealmIdMaxLength
        && realmId.All(char.IsAsciiDigit);

    private static Result Validate(string realmId, string? homeCurrency)
    {
        if (!IsValidRealmId(realmId))
        {
            return Result.Failure(QboConnectionErrors.RealmIdInvalid);
        }

        return string.Equals(homeCurrency?.Trim(), RequiredHomeCurrency, StringComparison.OrdinalIgnoreCase)
            ? Result.Success()
            : Result.Failure(QboConnectionErrors.HomeCurrencyNotCad);
    }

    private void Activate(
        string companyName,
        QboEnvironment environment,
        Guid connectedBy,
        DateTimeOffset refreshTokenExpiresAtUtc,
        DateTimeOffset now,
        bool isReconnect)
    {
        // Display only — QuickBooks allows longer names than the column, and a clipped name is
        // better than a refused connection.
        CompanyName = Truncate(string.IsNullOrWhiteSpace(companyName) ? "QuickBooks company" : companyName.Trim(), CompanyNameMaxLength);
        Environment = environment;
        Status = QboConnectionStatus.Active;
        ConnectedBy = connectedBy;
        ConnectedAtUtc = now;
        RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
        LastErrorCode = null;
        UpdatedAtUtc = now;

        Raise(new QboConnectedDomainEvent(Id, TenantId, RealmId, environment, connectedBy, isReconnect));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
