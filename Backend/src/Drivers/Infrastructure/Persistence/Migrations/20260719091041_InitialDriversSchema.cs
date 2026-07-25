using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDriversSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "drivers");

            migrationBuilder.CreateTable(
                name: "aggregate_snapshots",
                schema: "drivers",
                columns: table => new
                {
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    state = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aggregate_snapshots", x => new { x.aggregate_id, x.version });
                });

            migrationBuilder.CreateTable(
                name: "driver_clearances",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    granted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_clearances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "driver_credentials",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issued = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    optional = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_credentials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "drivers",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    licence_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    licence_expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    has_work_permit = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    registered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_journal",
                schema: "drivers",
                columns: table => new
                {
                    position = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_version = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_journal", x => x.position);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "drivers",
                columns: table => new
                {
                    position = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    routing_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispatched_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.position);
                });

            migrationBuilder.CreateTable(
                name: "projection_checkpoints",
                schema: "drivers",
                columns: table => new
                {
                    projection_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_position = table.Column<long>(type: "bigint", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projection_checkpoints", x => x.projection_name);
                });

            migrationBuilder.CreateTable(
                name: "rm_driver_clearances",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    client_name = table.Column<string>(type: "text", nullable: false),
                    expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    granted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_driver_clearances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_driver_credentials",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    issued = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    optional = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_driver_credentials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_drivers",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    licence_class = table.Column<string>(type: "text", nullable: false),
                    licence_expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    has_work_permit = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    registered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    credential_count = table.Column<int>(type: "integer", nullable: false),
                    soonest_credential_expiry = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_drivers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aggregate_snapshots_tenant_id_aggregate_type_created_at_utc",
                schema: "drivers",
                table: "aggregate_snapshots",
                columns: new[] { "tenant_id", "aggregate_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_driver_clearances_tenant_id_driver_id",
                schema: "drivers",
                table: "driver_clearances",
                columns: new[] { "tenant_id", "driver_id" });

            migrationBuilder.CreateIndex(
                name: "IX_driver_credentials_tenant_id_driver_id",
                schema: "drivers",
                table: "driver_credentials",
                columns: new[] { "tenant_id", "driver_id" });

            migrationBuilder.CreateIndex(
                name: "IX_drivers_tenant_id_name",
                schema: "drivers",
                table: "drivers",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_event_id",
                schema: "drivers",
                table: "event_journal",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_tenant_id_aggregate_id_aggregate_version",
                schema: "drivers",
                table: "event_journal",
                columns: new[] { "tenant_id", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_id",
                schema: "drivers",
                table: "outbox_messages",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "drivers",
                table: "outbox_messages",
                column: "position",
                filter: "dispatched_at_utc IS NULL");

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. The EF global
            // query filter in DriversDbContext is the API half; RLS is the backstop that holds
            // even for raw SQL or a buggy filter. FORCE makes the policy bind the table owner
            // too (migrations run as the app role, which owns these tables, so without FORCE
            // the policy would not apply to the role the API connects as). The session variable
            // app.tenant_id is set on connection open by TenantSessionInterceptor; NULLIF guards
            // the pooled-connection case where Npgsql's DISCARD ALL resets a custom GUC to the
            // empty string, not to unset (a bare ''::uuid cast would throw 22P02).
            //
            // Same policy shapes as Fleet/Trips (this module is born with the projection read
            // side, so the initial migration carries the full set at once):
            //
            // 1. Write tables get the tenant isolation policy plus an app.is_system SELECT
            //    bypass — the projection worker reads them on a pinned system session with no
            //    tenant, where a tenant-only policy would show it zero rows. Only the outbox
            //    dispatcher and the projection worker/rebuilder ever set app.is_system, both on
            //    pinned system connections, so request-session isolation is untouched.
            migrationBuilder.Sql(
                """
                ALTER TABLE drivers.drivers ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.drivers FORCE ROW LEVEL SECURITY;
                CREATE POLICY drivers_tenant_isolation ON drivers.drivers
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY drivers_system_read ON drivers.drivers
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.driver_credentials ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.driver_credentials FORCE ROW LEVEL SECURITY;
                CREATE POLICY driver_credentials_tenant_isolation ON drivers.driver_credentials
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY driver_credentials_system_read ON drivers.driver_credentials
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.driver_clearances ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.driver_clearances FORCE ROW LEVEL SECURITY;
                CREATE POLICY driver_clearances_tenant_isolation ON drivers.driver_clearances
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY driver_clearances_system_read ON drivers.driver_clearances
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                """);

            // 2. event_journal and aggregate_snapshots get SELECT + INSERT policies ONLY —
            //    the app role physically cannot rewrite audit history. The journal also gets
            //    the system SELECT bypass (the projection worker reads its cursor from it).
            migrationBuilder.Sql(
                """
                ALTER TABLE drivers.event_journal ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.event_journal FORCE ROW LEVEL SECURITY;
                CREATE POLICY event_journal_tenant_select ON drivers.event_journal
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_tenant_insert ON drivers.event_journal
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_system_read ON drivers.event_journal
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.aggregate_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.aggregate_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY aggregate_snapshots_tenant_select ON drivers.aggregate_snapshots
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY aggregate_snapshots_tenant_insert ON drivers.aggregate_snapshots
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);

            // 3. outbox_messages needs UPDATE (mark dispatched) by the OutboxDispatcher, a
            //    background service with no tenant. A second permissive policy keyed on
            //    app.is_system lets it through; request-path connections never set that
            //    variable, so tenant isolation holds for them.
            migrationBuilder.Sql(
                """
                ALTER TABLE drivers.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON drivers.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON drivers.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');
                """);

            // 4. rm_* projection tables — the same native RLS idiom as the write tables; the
            //    app.is_system arm lets the tenant-less projection worker and rebuilder write
            //    across tenants.
            migrationBuilder.Sql(
                """
                ALTER TABLE drivers.rm_drivers ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_drivers FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_drivers_tenant_isolation ON drivers.rm_drivers
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.rm_driver_credentials ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_driver_credentials FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_driver_credentials_tenant_isolation ON drivers.rm_driver_credentials
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.rm_driver_clearances ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_driver_clearances FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_driver_clearances_tenant_isolation ON drivers.rm_driver_clearances
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);

            // 5. projection_checkpoints — system-owned, spans tenants (no tenant_id). FORCE RLS
            //    even for the owning app role; a single policy admits only the system session
            //    (worker), which both reads the cursor and writes it. WITH CHECK defaults to
            //    the USING expression.
            migrationBuilder.Sql(
                """
                ALTER TABLE drivers.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON drivers.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aggregate_snapshots",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "driver_clearances",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "driver_credentials",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "drivers",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "event_journal",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_driver_clearances",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_driver_credentials",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_drivers",
                schema: "drivers");
        }
    }
}
