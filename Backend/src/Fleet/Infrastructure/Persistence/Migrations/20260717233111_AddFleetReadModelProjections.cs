using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Projection plumbing: the checkpoint table and the system-read policies the projection
    /// worker needs.
    ///
    /// HISTORY: as originally written this migration also created the read side as materialized
    /// views wrapped in security-barrier views owned by a northernlink_projector role. That role
    /// was only ever created by hand (docker/initdb/01-app-role.sql) and by the integration-test
    /// fixture, so this migration failed with 42704 ("role northernlink_projector does not exist")
    /// on every real database — it never applied anywhere. The matview DDL has therefore been
    /// removed rather than added-then-dropped: a later migration could not have rescued a fresh
    /// database, because migrations run in order and this one aborted first.
    ///
    /// The read side now lives in ReplaceFleetMatviewsWithProjectionTables as ordinary rm_*
    /// tables with native RLS. Everything kept below is still required by the projection worker.
    /// </summary>
    public partial class AddFleetReadModelProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Projection checkpoint — one row per module worker, system-owned (no tenant_id).
            // EF-generated CreateTable; RLS is hand-appended below.
            migrationBuilder.CreateTable(
                name: "projection_checkpoints",
                schema: "fleet",
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

            // The projector reads the write tables on a system session with no tenant, where the
            // tenant-only RLS policies would show it zero rows. An app.is_system read-bypass
            // policy (OR-ed with the existing tenant policy) on each base table, and on
            // event_journal (the worker reads its cursor the same way), admits exactly that.
            // Only OutboxDispatcher and the projection worker/rebuilder ever set app.is_system,
            // both on pinned system connections, so request-session isolation is untouched.
            migrationBuilder.Sql(
                """
                CREATE POLICY vehicles_system_read ON fleet.vehicles
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY retirement_certificates_system_read ON fleet.retirement_certificates
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY shops_system_read ON fleet.shops
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY vehicle_documents_system_read ON fleet.vehicle_documents
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY service_records_system_read ON fleet.service_records
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY work_orders_system_read ON fleet.work_orders
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY vehicle_inspections_system_read ON fleet.vehicle_inspections
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY event_journal_system_read ON fleet.event_journal
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                """);

            // projection_checkpoints — system-owned, spans tenants. FORCE RLS even for the
            // owning app role; a single policy admits only the system session (worker), which
            // both reads the cursor and writes it. WITH CHECK defaults to the USING expression.
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON fleet.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS vehicles_system_read ON fleet.vehicles;
                DROP POLICY IF EXISTS retirement_certificates_system_read ON fleet.retirement_certificates;
                DROP POLICY IF EXISTS shops_system_read ON fleet.shops;
                DROP POLICY IF EXISTS vehicle_documents_system_read ON fleet.vehicle_documents;
                DROP POLICY IF EXISTS service_records_system_read ON fleet.service_records;
                DROP POLICY IF EXISTS work_orders_system_read ON fleet.work_orders;
                DROP POLICY IF EXISTS vehicle_inspections_system_read ON fleet.vehicle_inspections;
                DROP POLICY IF EXISTS event_journal_system_read ON fleet.event_journal;
                """);

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "fleet");
        }
    }
}
