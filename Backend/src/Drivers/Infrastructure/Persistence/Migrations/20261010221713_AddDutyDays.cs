using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDutyDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "duty_days",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    last_sequence = table.Column<int>(type: "integer", nullable: false),
                    certified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    certified_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recertification_required = table.Column<bool>(type: "boolean", nullable: false),
                    personal_conveyance_km = table.Column<decimal>(type: "numeric(9,1)", precision: 9, scale: 1, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    events = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_duty_days", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_duty_days",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    driving_minutes = table.Column<int>(type: "integer", nullable: false),
                    on_duty_minutes = table.Column<int>(type: "integer", nullable: false),
                    off_duty_minutes = table.Column<int>(type: "integer", nullable: false),
                    pc_km = table.Column<decimal>(type: "numeric(9,1)", precision: 9, scale: 1, nullable: false),
                    last_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    certified = table.Column<bool>(type: "boolean", nullable: false),
                    certified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recertification_required = table.Column<bool>(type: "boolean", nullable: false),
                    violation_count = table.Column<int>(type: "integer", nullable: false),
                    event_count = table.Column<int>(type: "integer", nullable: false),
                    events = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_duty_days", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_duty_days_tenant_id_driver_id_local_date",
                schema: "drivers",
                table: "duty_days",
                columns: new[] { "tenant_id", "driver_id", "local_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_duty_days_tenant_id_driver_id_local_date",
                schema: "drivers",
                table: "rm_duty_days",
                columns: new[] { "tenant_id", "driver_id", "local_date" },
                unique: true);

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // Same shapes as the AddHosLogEntries tables. The write table gets the tenant
            // isolation policy plus the app.is_system SELECT bypass the projection worker needs
            // (it reads the write table on a pinned system session with no tenant). The rm_*
            // read table uses the combined-arm idiom so the tenant-less projection worker and
            // rebuilder can write across tenants. The audit/outbox/journal/checkpoint tables
            // already exist for the drivers schema — reused, not recreated.
            migrationBuilder.Sql(
                """
                ALTER TABLE drivers.duty_days ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.duty_days FORCE ROW LEVEL SECURITY;
                CREATE POLICY duty_days_tenant_isolation ON drivers.duty_days
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY duty_days_system_read ON drivers.duty_days
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.rm_duty_days ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_duty_days FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_duty_days_tenant_isolation ON drivers.rm_duty_days
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "duty_days",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_duty_days",
                schema: "drivers");
        }
    }
}
