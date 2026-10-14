using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Compliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "authority",
                schema: "compliance",
                table: "rule_sets",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "carrier_authorities",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nsc_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    current_sfc = table.Column<string>(type: "jsonb", nullable: true),
                    sfc_history = table.Column<string>(type: "jsonb", nullable: false),
                    safety_rating = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    safety_rating_as_of = table.Column<DateOnly>(type: "date", nullable: true),
                    intra_provincial = table.Column<bool>(type: "boolean", nullable: false),
                    intra_provincial_declared_on = table.Column<DateOnly>(type: "date", nullable: true),
                    intra_provincial_signature = table.Column<string>(type: "jsonb", nullable: true),
                    operating_routes = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carrier_authorities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "driver_lookup",
                schema: "compliance",
                columns: table => new
                {
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_lookup", x => x.driver_id);
                });

            migrationBuilder.CreateTable(
                name: "insurance_policies",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_unit_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    insurer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    liability_limit_cad = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    cargo_coverage_cad = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    minimum_liability_cad = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    effective_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    certificate = table.Column<string>(type: "jsonb", nullable: true),
                    filing_confirmation = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_on = table.Column<DateOnly>(type: "date", nullable: true),
                    compliance_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insurance_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_lookup",
                schema: "compliance",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    full_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_lookup", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_lookup",
                schema: "compliance",
                columns: table => new
                {
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    seating_capacity = table.Column<int>(type: "integer", nullable: false),
                    required_licence_class = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regulated_override = table.Column<bool>(type: "boolean", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_lookup", x => x.vehicle_id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_registrations",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_unit_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    plate = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    vehicle_class = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    term_months = table.Column<int>(type: "integer", nullable: false),
                    effective_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    document = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_on = table.Column<DateOnly>(type: "date", nullable: true),
                    compliance_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_registrations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_carrier_authorities_tenant_id",
                schema: "compliance",
                table: "carrier_authorities",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_driver_lookup_tenant_id",
                schema: "compliance",
                table: "driver_lookup",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_driver_lookup_user_id",
                schema: "compliance",
                table: "driver_lookup",
                column: "user_id",
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_insurance_policies_one_current_per_vehicle",
                schema: "compliance",
                table: "insurance_policies",
                columns: new[] { "tenant_id", "vehicle_id", "status" },
                unique: true,
                filter: "status = 'Current'");

            migrationBuilder.CreateIndex(
                name: "IX_insurance_policies_tenant_id_expires_on",
                schema: "compliance",
                table: "insurance_policies",
                columns: new[] { "tenant_id", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "IX_user_lookup_tenant_id",
                schema: "compliance",
                table: "user_lookup",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_lookup_tenant_id",
                schema: "compliance",
                table: "vehicle_lookup",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_one_current_per_vehicle",
                schema: "compliance",
                table: "vehicle_registrations",
                columns: new[] { "tenant_id", "vehicle_id", "status" },
                unique: true,
                filter: "status = 'Current'");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_tenant_id_expires_on",
                schema: "compliance",
                table: "vehicle_registrations",
                columns: new[] { "tenant_id", "expires_on" });

            // Row-Level Security — hand-appended; EF does not generate it. The database half of
            // tenant enforcement, the canonical shape of InitialComplianceSchema: ENABLE + FORCE
            // (so the table owner is not exempt) and one tenant policy keyed on the
            // app.tenant_id session variable that TenantSessionInterceptor sets, NULLIF-guarded so
            // an unset variable matches nothing instead of erroring.
            //
            // All six new tables are tenant tables and get only that policy:
            //  - carrier_authorities, insurance_policies, vehicle_registrations — aggregates written
            //    on the request path and by the record-stamp handler (which runs under the event's
            //    ambient tenant);
            //  - vehicle_lookup, driver_lookup, user_lookup — replicas upserted by the fan-out event
            //    handlers, each in a scope resolved under the event's tenant. No app.is_system arm:
            //    nothing reads them across tenants yet (the readiness worker of Epic 2 task 5 will
            //    add a system SELECT policy if it needs one).
            //
            // rule_sets only gained a column; its reference-data policies are unchanged.
            migrationBuilder.Sql(
                """
                ALTER TABLE compliance.carrier_authorities ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.carrier_authorities FORCE ROW LEVEL SECURITY;
                CREATE POLICY carrier_authorities_tenant_isolation ON compliance.carrier_authorities
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.insurance_policies ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.insurance_policies FORCE ROW LEVEL SECURITY;
                CREATE POLICY insurance_policies_tenant_isolation ON compliance.insurance_policies
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.vehicle_registrations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.vehicle_registrations FORCE ROW LEVEL SECURITY;
                CREATE POLICY vehicle_registrations_tenant_isolation ON compliance.vehicle_registrations
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.vehicle_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.vehicle_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY vehicle_lookup_tenant_isolation ON compliance.vehicle_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.driver_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.driver_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY driver_lookup_tenant_isolation ON compliance.driver_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.user_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.user_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY user_lookup_tenant_isolation ON compliance.user_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "carrier_authorities",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "driver_lookup",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "insurance_policies",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "user_lookup",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "vehicle_lookup",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "vehicle_registrations",
                schema: "compliance");

            migrationBuilder.DropColumn(
                name: "authority",
                schema: "compliance",
                table: "rule_sets");
        }
    }
}
