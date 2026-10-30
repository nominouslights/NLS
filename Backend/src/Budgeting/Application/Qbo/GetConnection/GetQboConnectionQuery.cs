using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Qbo.GetConnection;

/// <summary>The tenant's QuickBooks connection, as the QuickBooks screen shows it.</summary>
public sealed record GetQboConnectionQuery(Guid TenantId) : IQuery<QboConnectionResponse>;

/// <summary>
/// Wire shape of <c>GET /api/budgeting/qbo/connection</c>. Always 200: a tenant that has never
/// connected gets <see cref="Status"/> <c>"NotConnected"</c> with every other field null, rather
/// than a 404 the console would have to tell apart from a missing route.
/// <para>
/// <see cref="Status"/> is <c>NotConnected | Active | NeedsReconnect | Disconnected</c>.
/// Disconnected is reported only when there is no live connection and an earlier one was
/// disconnected — the company name it carries is that earlier company, which is also the only
/// company this tenant may reconnect to for now.
/// </para>
/// <para>
/// <see cref="ConnectedByName"/> / <see cref="ConnectedByEmail"/> resolve
/// <see cref="ConnectedBy"/> through the user replica, the <c>BudgetCodeResponse</c> convention:
/// the name is null when the person has not set one, and both are null for an id the replica
/// does not hold. <see cref="LastSyncAtUtc"/> is always null until the expense import ships.
/// No token, and nothing derived from one, is ever on this shape.
/// </para>
/// </summary>
public sealed record QboConnectionResponse(
    string Status,
    string? RealmId,
    string? CompanyName,
    string? Environment,
    Guid? ConnectedBy,
    string? ConnectedByName,
    string? ConnectedByEmail,
    DateTimeOffset? ConnectedAtUtc,
    DateTimeOffset? RefreshTokenExpiresAtUtc,
    DateTimeOffset? LastSyncAtUtc,
    string? LastErrorCode)
{
    public const string NotConnectedStatus = "NotConnected";

    public static QboConnectionResponse NotConnected { get; } =
        new(NotConnectedStatus, null, null, null, null, null, null, null, null, null, null);
}
