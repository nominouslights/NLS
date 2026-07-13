using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFleetSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fleet");

            migrationBuilder.CreateTable(
                name: "retirement_certificates",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    vin = table.Column<string>(type: "varchar(17)", nullable: false),
                    unit_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    make = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    final_odometer_km = table.Column<int>(type: "integer", nullable: false),
                    retirement_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    retired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retirement_certificates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    vin = table.Column<string>(type: "varchar(17)", nullable: false),
                    make = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    seating_capacity = table.Column<int>(type: "integer", nullable: false),
                    licence_plate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    required_licence_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    acquisition_cost_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    end_of_life_km = table.Column<int>(type: "integer", nullable: false),
                    sale_price_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    disposed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_retirement_certificates_tenant_id_certificate_number",
                schema: "fleet",
                table: "retirement_certificates",
                columns: new[] { "tenant_id", "certificate_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retirement_certificates_vehicle_id",
                schema: "fleet",
                table: "retirement_certificates",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_tenant_id_unit_number",
                schema: "fleet",
                table: "vehicles",
                columns: new[] { "tenant_id", "unit_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_tenant_id_vin",
                schema: "fleet",
                table: "vehicles",
                columns: new[] { "tenant_id", "vin" },
                unique: true);

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. The EF global
            // query filter in FleetDbContext is the API half; RLS is the backstop that holds
            // even for raw SQL or a buggy filter. FORCE makes the policy bind the table
            // owner too (the app role owns these tables in dev). The session variable
            // app.tenant_id is set on connection open by TenantSessionInterceptor; the
            // second argument `true` of current_setting makes it NULL (matching no rows)
            // when the variable is unset. Superusers still bypass RLS — which is why dev
            // connects as the non-superuser northernlink_app (docker/initdb/01-app-role.sql).
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.vehicles ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.vehicles FORCE ROW LEVEL SECURITY;
                CREATE POLICY vehicles_tenant_isolation ON fleet.vehicles
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid);

                ALTER TABLE fleet.retirement_certificates ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.retirement_certificates FORCE ROW LEVEL SECURITY;
                CREATE POLICY retirement_certificates_tenant_isolation ON fleet.retirement_certificates
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retirement_certificates",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "vehicles",
                schema: "fleet");
        }
    }
}
