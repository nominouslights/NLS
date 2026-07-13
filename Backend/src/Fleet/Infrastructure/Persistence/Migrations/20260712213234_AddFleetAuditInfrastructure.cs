using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetAuditInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "fleet",
                table: "vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "aggregate_snapshots",
                schema: "fleet",
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
                name: "event_journal",
                schema: "fleet",
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
                schema: "fleet",
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

            migrationBuilder.CreateIndex(
                name: "IX_aggregate_snapshots_tenant_id_aggregate_type_created_at_utc",
                schema: "fleet",
                table: "aggregate_snapshots",
                columns: new[] { "tenant_id", "aggregate_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_event_id",
                schema: "fleet",
                table: "event_journal",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_tenant_id_aggregate_id_aggregate_version",
                schema: "fleet",
                table: "event_journal",
                columns: new[] { "tenant_id", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_id",
                schema: "fleet",
                table: "outbox_messages",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "fleet",
                table: "outbox_messages",
                column: "position",
                filter: "dispatched_at_utc IS NULL");

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // Same pattern as InitialFleetSchema, with three deliberate differences:
            //
            // 1. event_journal and aggregate_snapshots get SELECT + INSERT policies ONLY.
            //    With RLS enabled and FORCEd, the absence of UPDATE/DELETE policies means
            //    the app role physically cannot rewrite audit history — append-only at the
            //    database level, exactly what an audit trail wants.
            //
            // 2. outbox_messages needs UPDATE (mark dispatched) by the OutboxDispatcher,
            //    a background service with no tenant. A second permissive policy keyed on
            //    the session variable app.is_system lets it through; request-path
            //    connections never set that variable, so tenant isolation holds for them.
            //
            // 3. NULLIF(current_setting(...), '') instead of a bare current_setting:
            //    Npgsql's pool reset (DISCARD ALL) resets a previously-set custom GUC to
            //    the EMPTY STRING, not to unset — so on a reused pooled connection a bare
            //    ''::uuid cast makes every RLS-checked statement throw 22P02 instead of
            //    cleanly matching no rows. NULLIF turns that back into NULL.
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.event_journal ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.event_journal FORCE ROW LEVEL SECURITY;
                CREATE POLICY event_journal_tenant_select ON fleet.event_journal
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_tenant_insert ON fleet.event_journal
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE fleet.aggregate_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.aggregate_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY aggregate_snapshots_tenant_select ON fleet.aggregate_snapshots
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY aggregate_snapshots_tenant_insert ON fleet.aggregate_snapshots
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE fleet.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON fleet.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON fleet.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');

                -- The InitialFleetSchema policies have the same pooled-connection bug (a
                -- bare ::uuid cast); recreate them with the NULLIF guard here rather than
                -- rewriting an already-applied migration.
                DROP POLICY vehicles_tenant_isolation ON fleet.vehicles;
                CREATE POLICY vehicles_tenant_isolation ON fleet.vehicles
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                DROP POLICY retirement_certificates_tenant_isolation ON fleet.retirement_certificates;
                CREATE POLICY retirement_certificates_tenant_isolation ON fleet.retirement_certificates
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aggregate_snapshots",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "event_journal",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "fleet");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "fleet",
                table: "vehicles");
        }
    }
}
