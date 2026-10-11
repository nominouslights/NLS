using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQboConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "qbo_connections",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    realm_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    company_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    connected_by = table.Column<Guid>(type: "uuid", nullable: false),
                    connected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    refresh_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_successful_sync_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_sync_cursor_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qbo_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qbo_oauth_states",
                schema: "budgeting",
                columns: table => new
                {
                    state_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qbo_oauth_states", x => x.state_hash);
                });

            migrationBuilder.CreateTable(
                name: "qbo_sync_runs",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    realm_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_full_sync = table.Column<bool>(type: "boolean", nullable: false),
                    cursor_from_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cursor_to_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lines_fetched = table.Column<int>(type: "integer", nullable: false),
                    lines_upserted = table.Column<int>(type: "integer", nullable: false),
                    lines_removed = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qbo_sync_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qbo_token_vault",
                schema: "budgeting",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    realm_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    access_token_cipher = table.Column<string>(type: "text", nullable: false),
                    refresh_token_cipher = table.Column<string>(type: "text", nullable: false),
                    access_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    refresh_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    key_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qbo_token_vault", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "rm_qbo_connections",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    realm_id = table.Column<string>(type: "text", nullable: false),
                    company_name = table.Column<string>(type: "text", nullable: false),
                    environment = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    connected_by = table.Column<Guid>(type: "uuid", nullable: false),
                    connected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    refresh_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_successful_sync_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_sync_cursor_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_qbo_connections", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_qbo_connections_one_live_per_tenant",
                schema: "budgeting",
                table: "qbo_connections",
                column: "tenant_id",
                unique: true,
                filter: "status <> 'Disconnected'");

            migrationBuilder.CreateIndex(
                name: "IX_qbo_connections_tenant_id_realm_id",
                schema: "budgeting",
                table: "qbo_connections",
                columns: new[] { "tenant_id", "realm_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qbo_oauth_states_tenant_id_expires_at_utc",
                schema: "budgeting",
                table: "qbo_oauth_states",
                columns: new[] { "tenant_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_qbo_sync_runs_one_running_per_tenant",
                schema: "budgeting",
                table: "qbo_sync_runs",
                column: "tenant_id",
                unique: true,
                filter: "status = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_qbo_sync_runs_tenant_id_started_at_utc",
                schema: "budgeting",
                table: "qbo_sync_runs",
                columns: new[] { "tenant_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_qbo_connections_tenant_id",
                schema: "budgeting",
                table: "rm_qbo_connections",
                column: "tenant_id");

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // EF never generates RLS. All five tables are tenant-scoped, so all five get a policy;
            // the shapes are AddBudgetAllocations', one per table kind, plus a third kind for the
            // two tables that must never be readable outside their own tenant:
            //
            //   qbo_connections (write side)   tenant policy + an app.is_system SELECT bypass, because
            //                                  the tenant-less projection worker reads write-side rows
            //                                  to build the read model.
            //   rm_qbo_connections (read side) one combined policy — tenant OR app.is_system — so the
            //                                  worker and the rebuilder can write across tenants.
            //   qbo_sync_runs                  the write-side shape: a scheduler enumerating tenants
            //                                  may read run history, never write it.
            //   qbo_token_vault                TENANT ONLY. No app.is_system policy of any kind: the
            //   qbo_oauth_states               vault holds encrypted QuickBooks tokens and the states
            //                                  bind OAuth round trips to a user. Nothing tenant-less
            //                                  ever needs either; the scheduled import pushes each
            //                                  tenant as the ambient tenant before touching tokens.
            //
            // FORCE is not optional: migrations run as the app role, which owns these tables, and an
            // unforced policy does not bind the owner. Neither is the NULLIF: current_setting returns
            // '' rather than NULL on a pooled connection Npgsql has reset, and casting '' to uuid
            // fails with 22P02 — which breaks every tenant-less session, not just that one.
            migrationBuilder.Sql(
                """
                ALTER TABLE budgeting.qbo_connections ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.qbo_connections FORCE ROW LEVEL SECURITY;
                CREATE POLICY qbo_connections_tenant_isolation ON budgeting.qbo_connections
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY qbo_connections_system_read ON budgeting.qbo_connections
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.rm_qbo_connections ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.rm_qbo_connections FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_qbo_connections_tenant_isolation ON budgeting.rm_qbo_connections
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.qbo_sync_runs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.qbo_sync_runs FORCE ROW LEVEL SECURITY;
                CREATE POLICY qbo_sync_runs_tenant_isolation ON budgeting.qbo_sync_runs
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY qbo_sync_runs_system_read ON budgeting.qbo_sync_runs
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.qbo_token_vault ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.qbo_token_vault FORCE ROW LEVEL SECURITY;
                CREATE POLICY qbo_token_vault_tenant_isolation ON budgeting.qbo_token_vault
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE budgeting.qbo_oauth_states ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.qbo_oauth_states FORCE ROW LEVEL SECURITY;
                CREATE POLICY qbo_oauth_states_tenant_isolation ON budgeting.qbo_oauth_states
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qbo_connections",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "qbo_oauth_states",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "qbo_sync_runs",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "qbo_token_vault",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "rm_qbo_connections",
                schema: "budgeting");
        }
    }
}
