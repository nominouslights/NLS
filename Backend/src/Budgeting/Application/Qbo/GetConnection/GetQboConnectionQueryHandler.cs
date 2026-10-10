using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Qbo;

namespace NorthernLink.Budgeting.Application.Qbo.GetConnection;

/// <summary>
/// Handles <see cref="GetQboConnectionQuery"/>. Reads the <b>write</b> table, not
/// <c>rm_qbo_connections</c>, on purpose: the console calls this immediately after
/// <c>POST qbo/connection/complete</c> and <c>DELETE qbo/connection</c>, and the projection
/// worker polls every few seconds, so the read model would show the state from before the
/// click. One small row per tenant makes the write-side read free.
/// </summary>
public sealed class GetQboConnectionQueryHandler(
    IQboConnectionRepository connections,
    IUserLookupRepository users)
    : IQueryHandler<GetQboConnectionQuery, QboConnectionResponse>
{
    public async Task<Result<QboConnectionResponse>> Handle(
        GetQboConnectionQuery query,
        CancellationToken cancellationToken)
    {
        var all = await connections.GetAllAsync(cancellationToken);

        var connection = all.FirstOrDefault(c => c.IsLive)
            ?? all.OrderByDescending(c => c.UpdatedAtUtc).FirstOrDefault();

        if (connection is null)
        {
            return Result.Success(QboConnectionResponse.NotConnected);
        }

        var connectedBy = await users.GetAsync(connection.ConnectedBy, cancellationToken);

        return Result.Success(new QboConnectionResponse(
            connection.Status.ToString(),
            connection.RealmId,
            connection.CompanyName,
            connection.Environment.ToString(),
            connection.ConnectedBy,
            connectedBy?.FullName,
            connectedBy?.Email,
            connection.ConnectedAtUtc,
            connection.Status == QboConnectionStatus.Disconnected ? null : connection.RefreshTokenExpiresAtUtc,
            connection.LastSuccessfulSyncAtUtc,
            connection.LastErrorCode));
    }
}
