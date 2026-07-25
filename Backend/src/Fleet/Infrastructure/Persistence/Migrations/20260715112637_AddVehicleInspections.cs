using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleInspections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_inspections",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    driver_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entered_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    trip_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    manifest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: true),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    checklist_items = table.Column<string>(type: "jsonb", nullable: true),
                    defects = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_inspections", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_inspections_tenant_id_manifest_id_type",
                schema: "fleet",
                table: "vehicle_inspections",
                columns: new[] { "tenant_id", "manifest_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_inspections_tenant_id_unit",
                schema: "fleet",
                table: "vehicle_inspections",
                columns: new[] { "tenant_id", "unit" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // Same pattern as the earlier Fleet migrations, using the NULLIF guard from
            // AddFleetAuditInfrastructure (Npgsql's pool reset turns an unset GUC into the
            // empty string; NULLIF turns that back into NULL so the policy cleanly matches
            // no rows instead of throwing 22P02). Rows are inserted by the
            // trips.trip-manifest-completed event consumer with the tenant taken from the
            // event payload and stamped on the aggregate before save.
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.vehicle_inspections ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.vehicle_inspections FORCE ROW LEVEL SECURITY;
                CREATE POLICY vehicle_inspections_tenant_isolation ON fleet.vehicle_inspections
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_inspections",
                schema: "fleet");
        }
    }
}
