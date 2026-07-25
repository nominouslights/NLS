using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHosLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "latest_driving_hours",
                schema: "drivers",
                table: "rm_drivers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "latest_duty_status",
                schema: "drivers",
                table: "rm_drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "latest_hos_date",
                schema: "drivers",
                table: "rm_drivers",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hos_log_entries",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    duty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    on_duty_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    driving_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    off_duty_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entered_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hos_log_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_hos_log_entries",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    duty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    on_duty_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    driving_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    off_duty_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entered_by = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_hos_log_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hos_log_entries_tenant_id_driver_id",
                schema: "drivers",
                table: "hos_log_entries",
                columns: new[] { "tenant_id", "driver_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_hos_log_entries_tenant_id_driver_id",
                schema: "drivers",
                table: "rm_hos_log_entries",
                columns: new[] { "tenant_id", "driver_id" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // Same shapes as the InitialDriversSchema tables. The write table gets the tenant
            // isolation policy plus the app.is_system SELECT bypass the projection worker needs
            // (it reads the write table on a pinned system session with no tenant). The rm_*
            // read table uses the combined-arm idiom so the tenant-less projection worker and
            // rebuilder can write across tenants. The audit/outbox/journal/checkpoint tables
            // already exist for the drivers schema — reused, not recreated.
            migrationBuilder.Sql(
                """
                ALTER TABLE drivers.hos_log_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.hos_log_entries FORCE ROW LEVEL SECURITY;
                CREATE POLICY hos_log_entries_tenant_isolation ON drivers.hos_log_entries
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY hos_log_entries_system_read ON drivers.hos_log_entries
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.rm_hos_log_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_hos_log_entries FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_hos_log_entries_tenant_isolation ON drivers.rm_hos_log_entries
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hos_log_entries",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_hos_log_entries",
                schema: "drivers");

            migrationBuilder.DropColumn(
                name: "latest_driving_hours",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "latest_duty_status",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "latest_hos_date",
                schema: "drivers",
                table: "rm_drivers");
        }
    }
}
