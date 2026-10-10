using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Budgeting.Domain.Qbo;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="QboConnection"/> into <c>budgeting.rm_qbo_connections</c>.</summary>
internal sealed class QboConnectionProjection : BudgetingProjection<QboConnection, QboConnectionReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(QboConnection));

    protected override void Map(QboConnection source, QboConnectionReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.RealmId = source.RealmId;
        row.CompanyName = source.CompanyName;
        row.Environment = source.Environment.ToString();
        row.Status = source.Status.ToString();
        row.ConnectedBy = source.ConnectedBy;
        row.ConnectedAtUtc = source.ConnectedAtUtc;
        row.RefreshTokenExpiresAtUtc = source.RefreshTokenExpiresAtUtc;
        row.LastSuccessfulSyncAtUtc = source.LastSuccessfulSyncAtUtc;
        row.LastSyncCursorUtc = source.LastSyncCursorUtc;
        row.LastErrorCode = source.LastErrorCode;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
