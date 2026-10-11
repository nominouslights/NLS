using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHosCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "prior_records_attested_at_utc",
                schema: "drivers",
                table: "rm_drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "prior_records_attested_by_user_id",
                schema: "drivers",
                table: "rm_drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "prior_records_covered_through_date",
                schema: "drivers",
                table: "rm_drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prior_records_note",
                schema: "drivers",
                table: "rm_drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "works_for_other_carriers",
                schema: "drivers",
                table: "rm_drivers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "prior_records_attested_at_utc",
                schema: "drivers",
                table: "drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "prior_records_attested_by_user_id",
                schema: "drivers",
                table: "drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "prior_records_covered_through_date",
                schema: "drivers",
                table: "drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prior_records_note",
                schema: "drivers",
                table: "drivers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "works_for_other_carriers",
                schema: "drivers",
                table: "drivers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "compliance_findings",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    rule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    follow_up_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    opened_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    opened_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_findings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "driver_out_of_service",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    declared_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lifted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lifted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lift_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_out_of_service", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "eld_malfunction_reports",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    noticed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reported_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eld_malfunction_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hos_exceptions",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: true),
                    declared_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    declared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    not_known_before_departure = table.Column<bool>(type: "boolean", nullable: false),
                    extra_driving = table.Column<TimeSpan>(type: "interval", nullable: true),
                    deferred_off_duty = table.Column<TimeSpan>(type: "interval", nullable: true),
                    eld_remark_recorded = table.Column<bool>(type: "boolean", nullable: false),
                    dispatch_acknowledged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dispatch_acknowledged_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closure_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hos_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_compliance_findings",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    rule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    follow_up_note = table.Column<string>(type: "text", nullable: true),
                    opened_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    opened_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_compliance_findings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_driver_out_of_service",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    declared_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    evidence = table.Column<string>(type: "text", nullable: true),
                    lifted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lifted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lift_reason = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_driver_out_of_service", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_eld_malfunction_reports",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_name = table.Column<string>(type: "text", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    noticed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reported_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_eld_malfunction_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_hos_exceptions",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    applying = table.Column<bool>(type: "boolean", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: true),
                    declared_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    declared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<string>(type: "text", nullable: true),
                    not_known_before_departure = table.Column<bool>(type: "boolean", nullable: false),
                    extra_driving_minutes = table.Column<int>(type: "integer", nullable: true),
                    deferred_off_duty_minutes = table.Column<int>(type: "integer", nullable: true),
                    eld_remark_recorded = table.Column<bool>(type: "boolean", nullable: false),
                    dispatch_acknowledged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dispatch_acknowledged_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closure_note = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_hos_exceptions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_findings_tenant_id_driver_id_local_date",
                schema: "drivers",
                table: "compliance_findings",
                columns: new[] { "tenant_id", "driver_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "IX_driver_out_of_service_tenant_id_driver_id_status",
                schema: "drivers",
                table: "driver_out_of_service",
                columns: new[] { "tenant_id", "driver_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_eld_malfunction_reports_tenant_id_driver_id_status",
                schema: "drivers",
                table: "eld_malfunction_reports",
                columns: new[] { "tenant_id", "driver_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_hos_exceptions_tenant_id_driver_id_effective_from_utc",
                schema: "drivers",
                table: "hos_exceptions",
                columns: new[] { "tenant_id", "driver_id", "effective_from_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_compliance_findings_tenant_id_driver_id_local_date",
                schema: "drivers",
                table: "rm_compliance_findings",
                columns: new[] { "tenant_id", "driver_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_driver_out_of_service_tenant_id_driver_id_status",
                schema: "drivers",
                table: "rm_driver_out_of_service",
                columns: new[] { "tenant_id", "driver_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_eld_malfunction_reports_tenant_id_driver_id_status",
                schema: "drivers",
                table: "rm_eld_malfunction_reports",
                columns: new[] { "tenant_id", "driver_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_hos_exceptions_tenant_id_driver_id_effective_from_utc",
                schema: "drivers",
                table: "rm_hos_exceptions",
                columns: new[] { "tenant_id", "driver_id", "effective_from_utc" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // Same shapes as AddDutyDays. Each write table gets the tenant isolation policy plus
            // the app.is_system SELECT bypass the projection worker needs (it reads the write
            // tables on a pinned system session with no tenant — the duty-day projection now
            // runs the engine over these records too). Each rm_* read table uses the
            // combined-arm idiom so the tenant-less projection worker and rebuilder can write
            // across tenants. drivers.drivers / rm_drivers already carry their policies; the new
            // columns on them need nothing. The audit/outbox/journal/checkpoint tables already
            // exist for the drivers schema — reused, not recreated.
            migrationBuilder.Sql(
                """
                ALTER TABLE drivers.hos_exceptions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.hos_exceptions FORCE ROW LEVEL SECURITY;
                CREATE POLICY hos_exceptions_tenant_isolation ON drivers.hos_exceptions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY hos_exceptions_system_read ON drivers.hos_exceptions
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.driver_out_of_service ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.driver_out_of_service FORCE ROW LEVEL SECURITY;
                CREATE POLICY driver_out_of_service_tenant_isolation ON drivers.driver_out_of_service
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY driver_out_of_service_system_read ON drivers.driver_out_of_service
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.eld_malfunction_reports ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.eld_malfunction_reports FORCE ROW LEVEL SECURITY;
                CREATE POLICY eld_malfunction_reports_tenant_isolation ON drivers.eld_malfunction_reports
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY eld_malfunction_reports_system_read ON drivers.eld_malfunction_reports
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.compliance_findings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.compliance_findings FORCE ROW LEVEL SECURITY;
                CREATE POLICY compliance_findings_tenant_isolation ON drivers.compliance_findings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY compliance_findings_system_read ON drivers.compliance_findings
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.rm_hos_exceptions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_hos_exceptions FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_hos_exceptions_tenant_isolation ON drivers.rm_hos_exceptions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.rm_driver_out_of_service ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_driver_out_of_service FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_driver_out_of_service_tenant_isolation ON drivers.rm_driver_out_of_service
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.rm_eld_malfunction_reports ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_eld_malfunction_reports FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_eld_malfunction_reports_tenant_isolation ON drivers.rm_eld_malfunction_reports
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE drivers.rm_compliance_findings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE drivers.rm_compliance_findings FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_compliance_findings_tenant_isolation ON drivers.rm_compliance_findings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compliance_findings",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "driver_out_of_service",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "eld_malfunction_reports",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "hos_exceptions",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_compliance_findings",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_driver_out_of_service",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_eld_malfunction_reports",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "rm_hos_exceptions",
                schema: "drivers");

            migrationBuilder.DropColumn(
                name: "prior_records_attested_at_utc",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "prior_records_attested_by_user_id",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "prior_records_covered_through_date",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "prior_records_note",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "works_for_other_carriers",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "prior_records_attested_at_utc",
                schema: "drivers",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "prior_records_attested_by_user_id",
                schema: "drivers",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "prior_records_covered_through_date",
                schema: "drivers",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "prior_records_note",
                schema: "drivers",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "works_for_other_carriers",
                schema: "drivers",
                table: "drivers");
        }
    }
}
