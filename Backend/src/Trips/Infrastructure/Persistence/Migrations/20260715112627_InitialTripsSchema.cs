using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTripsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trips");

            migrationBuilder.CreateTable(
                name: "aggregate_snapshots",
                schema: "trips",
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
                schema: "trips",
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
                schema: "trips",
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
                name: "trip_manifests",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_date = table.Column<DateOnly>(type: "date", nullable: false),
                    trip_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    client = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    driver_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    driver_licence_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    licence_plate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    odometer_start_km = table.Column<int>(type: "integer", nullable: true),
                    fuel_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    weather = table.Column<string[]>(type: "text[]", nullable: false),
                    temperature_c = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    road_conditions = table.Column<string[]>(type: "text[]", nullable: false),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    road_advisories = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    all_seatbelts_verified = table.Column<bool>(type: "boolean", nullable: false),
                    all_cargo_secured = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    issues = table.Column<List<string>>(type: "text[]", nullable: false),
                    no_issues = table.Column<bool>(type: "boolean", nullable: false),
                    departure_time = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    arrival_time = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    odometer_end_km = table.Column<int>(type: "integer", nullable: true),
                    total_km = table.Column<int>(type: "integer", nullable: true),
                    fuel_added = table.Column<bool>(type: "boolean", nullable: false),
                    fuel_litres = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    fuel_cost_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    attestations = table.Column<List<bool>>(type: "boolean[]", nullable: false),
                    driver_signature_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    certified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    entered_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    entered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    cargo = table.Column<string>(type: "jsonb", nullable: true),
                    passengers = table.Column<string>(type: "jsonb", nullable: true),
                    post_trip_items = table.Column<string>(type: "jsonb", nullable: true),
                    pre_trip_items = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_manifests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aggregate_snapshots_tenant_id_aggregate_type_created_at_utc",
                schema: "trips",
                table: "aggregate_snapshots",
                columns: new[] { "tenant_id", "aggregate_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_event_id",
                schema: "trips",
                table: "event_journal",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_tenant_id_aggregate_id_aggregate_version",
                schema: "trips",
                table: "event_journal",
                columns: new[] { "tenant_id", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_id",
                schema: "trips",
                table: "outbox_messages",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "trips",
                table: "outbox_messages",
                column: "position",
                filter: "dispatched_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_trip_manifests_tenant_id_trip_number",
                schema: "trips",
                table: "trip_manifests",
                columns: new[] { "tenant_id", "trip_number" });

            migrationBuilder.CreateIndex(
                name: "IX_trip_manifests_tenant_id_unit",
                schema: "trips",
                table: "trip_manifests",
                columns: new[] { "tenant_id", "unit" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. The EF global
            // query filter in TripsDbContext is the API half; RLS is the backstop that holds
            // even for raw SQL or a buggy filter. FORCE makes the policy bind the table
            // owner too (the app role owns these tables in dev). The session variable
            // app.tenant_id is set on connection open by TenantSessionInterceptor.
            //
            // Same policy shapes as Fleet's migrations (this module starts on the corrected
            // NULLIF form — Npgsql's pool reset (DISCARD ALL) resets a previously-set custom
            // GUC to the EMPTY STRING, not to unset, so a bare ''::uuid cast would make every
            // RLS-checked statement throw 22P02 on a reused pooled connection):
            //
            // 1. event_journal and aggregate_snapshots get SELECT + INSERT policies ONLY —
            //    the app role physically cannot rewrite audit history.
            //
            // 2. outbox_messages needs UPDATE (mark dispatched) by the OutboxDispatcher,
            //    a background service with no tenant. A second permissive policy keyed on
            //    the session variable app.is_system lets it through; request-path
            //    connections never set that variable, so tenant isolation holds for them.
            migrationBuilder.Sql(
                """
                ALTER TABLE trips.trip_manifests ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.trip_manifests FORCE ROW LEVEL SECURITY;
                CREATE POLICY trip_manifests_tenant_isolation ON trips.trip_manifests
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE trips.event_journal ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.event_journal FORCE ROW LEVEL SECURITY;
                CREATE POLICY event_journal_tenant_select ON trips.event_journal
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_tenant_insert ON trips.event_journal
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE trips.aggregate_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.aggregate_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY aggregate_snapshots_tenant_select ON trips.aggregate_snapshots
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY aggregate_snapshots_tenant_insert ON trips.aggregate_snapshots
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE trips.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON trips.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON trips.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aggregate_snapshots",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "event_journal",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "trip_manifests",
                schema: "trips");
        }
    }
}
