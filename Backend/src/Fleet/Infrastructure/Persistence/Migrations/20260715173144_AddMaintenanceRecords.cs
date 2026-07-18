using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "trip_number",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<Guid>(
                name: "manifest_id",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "generated_work_order_id",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_records",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    performed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    items_changed = table.Column<List<string>>(type: "text[]", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    labor_hours = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    cost_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    parts_used = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_documents",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    file_size_kb = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_ref = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_to = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    line_items = table.Column<List<string>>(type: "text[]", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolving_service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorized_limit_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    budget_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    date_required_or_oos = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_records_tenant_id_number",
                schema: "fleet",
                table: "service_records",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_records_tenant_id_vehicle_id",
                schema: "fleet",
                table: "service_records",
                columns: new[] { "tenant_id", "vehicle_id" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_documents_tenant_id_number",
                schema: "fleet",
                table: "vehicle_documents",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_documents_tenant_id_vehicle_id",
                schema: "fleet",
                table: "vehicle_documents",
                columns: new[] { "tenant_id", "vehicle_id" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_tenant_id_number",
                schema: "fleet",
                table: "work_orders",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_tenant_id_vehicle_id",
                schema: "fleet",
                table: "work_orders",
                columns: new[] { "tenant_id", "vehicle_id" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // Standard tenant-isolation policy per new table, using the NULLIF guard from
            // AddFleetAuditInfrastructure (Npgsql's pool reset turns an unset GUC into the
            // empty string; NULLIF turns that back into NULL so the policy cleanly matches
            // no rows instead of throwing 22P02). FORCE is required because the app role owns
            // the tables. Owned jsonb collections (parts_used) live inline on their parent row,
            // so they inherit the parent's policy automatically.
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.service_records ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.service_records FORCE ROW LEVEL SECURITY;
                CREATE POLICY service_records_tenant_isolation ON fleet.service_records
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE fleet.vehicle_documents ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.vehicle_documents FORCE ROW LEVEL SECURITY;
                CREATE POLICY vehicle_documents_tenant_isolation ON fleet.vehicle_documents
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE fleet.work_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.work_orders FORCE ROW LEVEL SECURITY;
                CREATE POLICY work_orders_tenant_isolation ON fleet.work_orders
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_records",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "vehicle_documents",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "work_orders",
                schema: "fleet");

            migrationBuilder.DropColumn(
                name: "generated_work_order_id",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.AlterColumn<string>(
                name: "trip_number",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "manifest_id",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
