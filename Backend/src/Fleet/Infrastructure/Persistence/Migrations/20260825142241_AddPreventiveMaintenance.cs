using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Preventative maintenance: plans (items + overhauls as jsonb), per-vehicle plan
    /// assignments, and the append-only completion log — plus their rm_* projection tables.
    /// RLS is hand-appended at the end of Up with a per-table shape: tenant policies on the
    /// write tables, an append-only shape on pm_completions, system read where the projection
    /// worker needs it, and the two-arm write policy only on the rm_* tables it writes.
    /// The rm_* indexes are deliberately non-unique (uniqueness lives on the write tables
    /// only — a unique index on an eventually-consistent projection can wedge the worker).
    /// </summary>
    public partial class AddPreventiveMaintenance : Migration
    {
        private static readonly string[] ReadModelTables =
        [
            "rm_maintenance_plans",
            "rm_pm_plan_assignments",
            "rm_pm_completions",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "maintenance_plans",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vehicle_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_class = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: true),
                    overhauls = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pm_completions",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    performed_at = table.Column<DateOnly>(type: "date", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    performed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    measurement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_completions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pm_plan_assignments",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_plan_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_maintenance_plans",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    vehicle_model = table.Column<string>(type: "text", nullable: false),
                    service_class = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: true),
                    overhauls = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_maintenance_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_pm_completions",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    performed_at = table.Column<DateOnly>(type: "date", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    performed_by = table.Column<string>(type: "text", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    measurement = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_pm_completions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_pm_plan_assignments",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_pm_plan_assignments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_plans_tenant_id_name",
                schema: "fleet",
                table: "maintenance_plans",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_assignments_tenant_id_vehicle_id",
                schema: "fleet",
                table: "pm_plan_assignments",
                columns: new[] { "tenant_id", "vehicle_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_maintenance_plans_tenant_id_name",
                schema: "fleet",
                table: "rm_maintenance_plans",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_pm_completions_tenant_id_vehicle_id_performed_at",
                schema: "fleet",
                table: "rm_pm_completions",
                columns: new[] { "tenant_id", "vehicle_id", "performed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_rm_pm_plan_assignments_tenant_id_plan_id",
                schema: "fleet",
                table: "rm_pm_plan_assignments",
                columns: new[] { "tenant_id", "plan_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_pm_plan_assignments_tenant_id_vehicle_id",
                schema: "fleet",
                table: "rm_pm_plan_assignments",
                columns: new[] { "tenant_id", "vehicle_id" });

            // ---- Tenant isolation, DB half — hand-appended, same idiom as the rest of the
            // module (NULLIF guards the pooled-connection case where the session variable is
            // reset to the empty string). FORCE binds the owner too: migrations run as
            // northernlink_app, which therefore owns these tables, so without FORCE the
            // policies would not apply to the very role the API connects as. No GRANTs are
            // needed for the same reason — the app already owns them.
            //
            // The shapes differ per table, deliberately:
            //
            // 1. maintenance_plans / pm_plan_assignments: request-path writes are tenant-only
            //    (same as work_orders in AddMaintenanceRecords); the tenant-less projection
            //    worker only ever READS the write side, so it gets a separate FOR SELECT
            //    system policy (same as AddFleetReadModelProjections) — never a system arm on
            //    the ALL policy, which would let a system session write across tenants.
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.maintenance_plans ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.maintenance_plans FORCE ROW LEVEL SECURITY;
                CREATE POLICY maintenance_plans_tenant_isolation ON fleet.maintenance_plans
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY maintenance_plans_system_read ON fleet.maintenance_plans
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE fleet.pm_plan_assignments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.pm_plan_assignments FORCE ROW LEVEL SECURITY;
                CREATE POLICY pm_plan_assignments_tenant_isolation ON fleet.pm_plan_assignments
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY pm_plan_assignments_system_read ON fleet.pm_plan_assignments
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                """);

            // 2. pm_completions is the append-only PM ledger, so it gets the event-journal
            //    shape from AddFleetAuditInfrastructure: tenant SELECT + tenant INSERT and no
            //    UPDATE/DELETE policy at all — with RLS FORCEd, the app role physically cannot
            //    rewrite completion history. The projection worker reads it via the same
            //    FOR SELECT system policy as the other write tables.
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.pm_completions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.pm_completions FORCE ROW LEVEL SECURITY;
                CREATE POLICY pm_completions_tenant_select ON fleet.pm_completions
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY pm_completions_tenant_insert ON fleet.pm_completions
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY pm_completions_system_read ON fleet.pm_completions
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                """);

            // 3. rm_* projection tables: the tenant-less projection worker and rebuilder
            //    UPSERT into these as system, so here — and only here — the ALL policy carries
            //    the app.is_system arm (same as ReplaceFleetMatviewsWithProjectionTables).
            foreach (var table in ReadModelTables)
            {
                migrationBuilder.Sql(
                    $"""
                    ALTER TABLE fleet.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE fleet.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY {table}_tenant_isolation ON fleet.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                               OR current_setting('app.is_system', true) = 'true');
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maintenance_plans",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "pm_completions",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "pm_plan_assignments",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_maintenance_plans",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_pm_completions",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_pm_plan_assignments",
                schema: "fleet");
        }
    }
}
