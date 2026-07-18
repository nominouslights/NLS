using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripsReadModelProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Projection checkpoint — one row per module worker, system-owned (no tenant_id).
            migrationBuilder.CreateTable(
                name: "projection_checkpoints",
                schema: "trips",
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

            // ---- Hand-appended: read-side materialized view + tenant backstop ----
            // Same two-role backstop pattern as fleet's AddFleetReadModelProjections; Trips has
            // one pass-through matview (jsonb/array columns carried verbatim) and no derived
            // columns, so no round_even helper is needed here.

            migrationBuilder.Sql(
                """
                CREATE MATERIALIZED VIEW trips.mv_trip_manifests AS
                    SELECT * FROM trips.trip_manifests WITH NO DATA;

                CREATE UNIQUE INDEX ix_mv_trip_manifests_id ON trips.mv_trip_manifests (id);
                """);

            // app.is_system read-bypass so a system-session refresh (and the worker's journal
            // cursor read) see every tenant's rows; OR-ed with the existing tenant policy.
            migrationBuilder.Sql(
                """
                CREATE POLICY trip_manifests_system_read ON trips.trip_manifests
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY event_journal_system_read ON trips.event_journal
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON trips.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');
                """);

            // Two-role backstop: matview + wrapper owned by projector; app gets SELECT on the
            // wrapper only (app is a NOINHERIT member of projector, so it can SET ROLE to refresh
            // but cannot read the matview directly). Ordering matters: create the wrapper while
            // app still owns the matview, THEN hand both to the projector and revoke app's direct
            // matview privilege. Projector needs SELECT on the base table (refresh runs as owner)
            // and USAGE + CREATE on the schema (to receive ownership and resolve names).
            migrationBuilder.Sql(
                """
                GRANT USAGE, CREATE ON SCHEMA trips TO northernlink_projector;
                GRANT SELECT ON trips.trip_manifests TO northernlink_projector;

                CREATE VIEW trips.v_trip_manifests WITH (security_barrier = true) AS
                    SELECT * FROM trips.mv_trip_manifests
                    WHERE tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid;

                ALTER MATERIALIZED VIEW trips.mv_trip_manifests OWNER TO northernlink_projector;
                ALTER VIEW trips.v_trip_manifests OWNER TO northernlink_projector;

                -- Grant app SELECT on the wrapper AS the projector (owner); app is a NOINHERIT
                -- member so it cannot grant on the handed-over object itself, nor read the matview
                -- directly. No REVOKE needed — ownership transfer already stripped app's rights.
                SET ROLE northernlink_projector;
                GRANT SELECT ON trips.v_trip_manifests TO northernlink_app;
                RESET ROLE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP VIEW IF EXISTS trips.v_trip_manifests;
                DROP MATERIALIZED VIEW IF EXISTS trips.mv_trip_manifests;
                DROP POLICY IF EXISTS trip_manifests_system_read ON trips.trip_manifests;
                DROP POLICY IF EXISTS event_journal_system_read ON trips.event_journal;
                """);

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "trips");
        }
    }
}
