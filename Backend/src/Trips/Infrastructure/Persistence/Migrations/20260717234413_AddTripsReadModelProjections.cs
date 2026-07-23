using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Projection plumbing: the checkpoint table and the system-read policies the projection
    /// worker needs.
    ///
    /// HISTORY: as originally written this migration also created the read side as a materialized
    /// view wrapped in a security-barrier view owned by a northernlink_projector role — the same
    /// pattern as Fleet's AddFleetReadModelProjections, and the same failure: that role was only
    /// ever created by hand, so this migration aborted with 42704 and never applied anywhere. The
    /// matview DDL is removed rather than added-then-dropped, because migrations run in order and
    /// a later migration cannot rescue a fresh database from an earlier one that aborts.
    ///
    /// The read side now lives in ReplaceTripsMatviewWithProjectionTable as an ordinary
    /// rm_trip_manifests table with native RLS.
    /// </summary>
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

            // app.is_system read-bypass so the projector (and the worker's journal cursor read)
            // see every tenant's rows; OR-ed with the existing tenant policy.
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS trip_manifests_system_read ON trips.trip_manifests;
                DROP POLICY IF EXISTS event_journal_system_read ON trips.event_journal;
                """);

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "trips");
        }
    }
}
