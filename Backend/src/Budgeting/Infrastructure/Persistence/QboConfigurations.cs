using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Application.Qbo;
using NorthernLink.Budgeting.Domain.Qbo;
using NorthernLink.Budgeting.Infrastructure.Qbo;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Maps the QboConnection aggregate to budgeting.qbo_connections. No token column, ever.</summary>
public sealed class QboConnectionConfiguration : IEntityTypeConfiguration<QboConnection>
{
    public void Configure(EntityTypeBuilder<QboConnection> builder)
    {
        builder.ToTable("qbo_connections");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.RealmId).HasColumnName("realm_id").HasMaxLength(QboConnection.RealmIdMaxLength);
        builder.Property(c => c.CompanyName).HasColumnName("company_name").HasMaxLength(QboConnection.CompanyNameMaxLength);
        builder.Property(c => c.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.ConnectedBy).HasColumnName("connected_by");
        builder.Property(c => c.ConnectedAtUtc).HasColumnName("connected_at_utc");
        builder.Property(c => c.RefreshTokenExpiresAtUtc).HasColumnName("refresh_token_expires_at_utc");
        builder.Property(c => c.LastSuccessfulSyncAtUtc).HasColumnName("last_successful_sync_at_utc");
        builder.Property(c => c.LastSyncCursorUtc).HasColumnName("last_sync_cursor_utc");
        builder.Property(c => c.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(QboConnection.ErrorCodeMaxLength);
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // At most one live connection per tenant. Partial, so Disconnected rows stay as history.
        builder.HasIndex(c => c.TenantId)
            .IsUnique()
            .HasFilter("status <> 'Disconnected'")
            .HasDatabaseName("ix_qbo_connections_one_live_per_tenant");

        // One row per (tenant, company): a reconnect revives the row instead of adding one.
        builder.HasIndex(c => new { c.TenantId, c.RealmId }).IsUnique();

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext.
    }
}

/// <summary>
/// Maps <see cref="QboTokenVaultEntry"/> to budgeting.qbo_token_vault — a plain keyed table,
/// outside the audit pipeline (no version, no snapshot, no journal).
/// </summary>
public sealed class QboTokenVaultConfiguration : IEntityTypeConfiguration<QboTokenVaultEntry>
{
    public void Configure(EntityTypeBuilder<QboTokenVaultEntry> builder)
    {
        builder.ToTable("qbo_token_vault");

        builder.HasKey(e => e.TenantId);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(e => e.RealmId).HasColumnName("realm_id").HasMaxLength(QboConnection.RealmIdMaxLength);

        // Unbounded: Intuit does not commit to a token length, and a clipped cipher is a lost grant.
        builder.Property(e => e.AccessTokenCipher).HasColumnName("access_token_cipher");
        builder.Property(e => e.RefreshTokenCipher).HasColumnName("refresh_token_cipher");

        builder.Property(e => e.AccessTokenExpiresAtUtc).HasColumnName("access_token_expires_at_utc");
        builder.Property(e => e.RefreshTokenExpiresAtUtc).HasColumnName("refresh_token_expires_at_utc");
        builder.Property(e => e.KeyId).HasColumnName("key_id").HasMaxLength(32);
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}

/// <summary>Maps <see cref="QboOAuthState"/> to budgeting.qbo_oauth_states (plain, not audited).</summary>
public sealed class QboOAuthStateConfiguration : IEntityTypeConfiguration<QboOAuthState>
{
    public void Configure(EntityTypeBuilder<QboOAuthState> builder)
    {
        builder.ToTable("qbo_oauth_states");

        builder.HasKey(s => s.StateHash);
        builder.Property(s => s.StateHash).HasColumnName("state_hash").HasMaxLength(64).ValueGeneratedNever();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(s => s.ConsumedAtUtc).HasColumnName("consumed_at_utc");

        builder.HasIndex(s => new { s.TenantId, s.ExpiresAtUtc });
    }
}

/// <summary>
/// One run of the QuickBooks expense import, in budgeting.qbo_sync_runs. Created now, written by
/// the import (a later slice): it is both the run log the console lists and the lease that keeps a
/// manual "Sync now" and the scheduled run from overlapping — the partial unique index admits one
/// <c>Running</c> row per tenant, and <see cref="LeaseExpiresAtUtc"/> lets a run whose process
/// died be taken over rather than block the tenant forever.
/// </summary>
public sealed class QboSyncRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RealmId { get; set; } = null!;

    /// <summary><c>Manual</c> or <c>Scheduled</c>.</summary>
    public string Trigger { get; set; } = null!;

    /// <summary><c>Running</c>, <c>Succeeded</c>, <c>Failed</c> or <c>Abandoned</c>.</summary>
    public string Status { get; set; } = null!;

    /// <summary>Which process holds the lease (host name + process id).</summary>
    public string LeaseOwner { get; set; } = null!;

    public DateTimeOffset LeaseExpiresAtUtc { get; set; }
    public Guid? RequestedBy { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>True for a full query-based import, false for an incremental change-data-capture one.</summary>
    public bool IsFullSync { get; set; }

    public DateTimeOffset? CursorFromUtc { get; set; }
    public DateTimeOffset? CursorToUtc { get; set; }
    public int LinesFetched { get; set; }
    public int LinesUpserted { get; set; }
    public int LinesRemoved { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>Maps <see cref="QboSyncRun"/> to budgeting.qbo_sync_runs (plain, not audited).</summary>
public sealed class QboSyncRunConfiguration : IEntityTypeConfiguration<QboSyncRun>
{
    public void Configure(EntityTypeBuilder<QboSyncRun> builder)
    {
        builder.ToTable("qbo_sync_runs");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.RealmId).HasColumnName("realm_id").HasMaxLength(QboConnection.RealmIdMaxLength);
        builder.Property(r => r.Trigger).HasColumnName("trigger").HasMaxLength(16);
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(r => r.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(128);
        builder.Property(r => r.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
        builder.Property(r => r.RequestedBy).HasColumnName("requested_by");
        builder.Property(r => r.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(r => r.FinishedAtUtc).HasColumnName("finished_at_utc");
        builder.Property(r => r.IsFullSync).HasColumnName("is_full_sync");
        builder.Property(r => r.CursorFromUtc).HasColumnName("cursor_from_utc");
        builder.Property(r => r.CursorToUtc).HasColumnName("cursor_to_utc");
        builder.Property(r => r.LinesFetched).HasColumnName("lines_fetched");
        builder.Property(r => r.LinesUpserted).HasColumnName("lines_upserted");
        builder.Property(r => r.LinesRemoved).HasColumnName("lines_removed");
        builder.Property(r => r.ErrorCode).HasColumnName("error_code").HasMaxLength(QboConnection.ErrorCodeMaxLength);

        // The lease: one Running run per tenant.
        builder.HasIndex(r => r.TenantId)
            .IsUnique()
            .HasFilter("status = 'Running'")
            .HasDatabaseName("ix_qbo_sync_runs_one_running_per_tenant");

        builder.HasIndex(r => new { r.TenantId, r.StartedAtUtc });
    }
}
