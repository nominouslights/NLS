namespace NorthernLink.Budgeting.Domain.Qbo;

/// <summary>
/// Where a tenant's QuickBooks Online connection stands. Persisted by name (<c>varchar(16)</c>),
/// so members must never be renamed once a row has been written in that state.
/// <list type="bullet">
/// <item><description><see cref="Active"/>: tokens are held and were last known to work.</description></item>
/// <item><description><see cref="NeedsReconnect"/>: Intuit refused the refresh token
/// (<c>invalid_grant</c>) or the stored tokens can no longer be decrypted. Someone with
/// BudgetAccess has to run the OAuth flow again.</description></item>
/// <item><description><see cref="Disconnected"/>: a person disconnected it. The row stays as
/// history; the token vault row is gone.</description></item>
/// </list>
/// </summary>
public enum QboConnectionStatus
{
    Active,
    NeedsReconnect,
    Disconnected,
}
