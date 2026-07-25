using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Replaces the materialized-view read side with ordinary projector-maintained tables.
    ///
    /// WHY: Postgres cannot attach RLS policies to a materialized view, so tenant isolation had
    /// to be faked — the matviews were owned by a dedicated northernlink_projector role and
    /// wrapped in security_barrier views that filtered on app.tenant_id, with the app role granted
    /// SELECT on the wrappers only. That indirection is what made AddFleetReadModelProjections
    /// fail with 42704 ("role northernlink_projector does not exist") on any database where the
    /// role had not been provisioned by hand. Plain tables carry the same native RLS policy as
    /// every other table on the platform, so the second role — and that whole class of
    /// migration-ordering bug — disappears.
    ///
    /// Read models are derived data: dropping them loses nothing, and ProjectionRebuilder
    /// repopulates every row from the write tables.
    /// </summary>
    public partial class ReplaceFleetMatviewsWithProjectionTables : Migration
    {
        private static readonly string[] ReadModelTables =
        [
            "rm_vehicles",
            "rm_retirement_certificates",
            "rm_shops",
            "rm_vehicle_documents",
            "rm_service_records",
            "rm_work_orders",
            "rm_vehicle_inspections",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Tear down the matview read side (wrappers first — they depend on the matviews).
            // Dropping the objects also drops every grant and ownership tie to
            // northernlink_projector, which nothing references after this migration.
            migrationBuilder.Sql(
                """
                DROP VIEW IF EXISTS fleet.v_vehicles;
                DROP VIEW IF EXISTS fleet.v_retirement_certificates;
                DROP VIEW IF EXISTS fleet.v_shops;
                DROP VIEW IF EXISTS fleet.v_vehicle_documents;
                DROP VIEW IF EXISTS fleet.v_service_records;
                DROP VIEW IF EXISTS fleet.v_work_orders;
                DROP VIEW IF EXISTS fleet.v_vehicle_inspections;

                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_vehicles;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_retirement_certificates;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_shops;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_vehicle_documents;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_service_records;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_work_orders;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_vehicle_inspections;

                -- Existed only so the matviews' SQL could reproduce C#'s MidpointRounding.ToEven.
                -- The projector now writes the aggregate's own computed values, so there is a
                -- single implementation and nothing left to keep in parity.
                DROP FUNCTION IF EXISTS fleet.round_even(numeric, int);
                """);

            migrationBuilder.CreateTable(
                name: "rm_retirement_certificates",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate_number = table.Column<string>(type: "text", nullable: false),
                    vin = table.Column<string>(type: "text", nullable: false),
                    unit_number = table.Column<string>(type: "text", nullable: false),
                    make = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    final_odometer_km = table.Column<int>(type: "integer", nullable: false),
                    retirement_reason = table.Column<string>(type: "text", nullable: false),
                    retired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_retirement_certificates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_service_records",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    performed_by = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    items_changed = table.Column<List<string>>(type: "text[]", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    labor_hours = table.Column<decimal>(type: "numeric", nullable: true),
                    cost_cad = table.Column<decimal>(type: "numeric", nullable: true),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    parts_used = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_service_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_shops",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    contact_name = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    gst_business_no = table.Column<string>(type: "text", nullable: true),
                    mpi_accredited = table.Column<bool>(type: "boolean", nullable: false),
                    inspection_station_no = table.Column<string>(type: "text", nullable: true),
                    supplies_parts = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_shops", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_vehicle_documents",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_size_kb = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by = table.Column<string>(type: "text", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_vehicle_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_vehicle_inspections",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    driver_name = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    entered_by = table.Column<string>(type: "text", nullable: true),
                    trip_number = table.Column<string>(type: "text", nullable: true),
                    manifest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generated_work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: true),
                    result = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    checklist_items = table.Column<string>(type: "jsonb", nullable: true),
                    defects = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_vehicle_inspections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_vehicles",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_number = table.Column<string>(type: "text", nullable: false),
                    vin = table.Column<string>(type: "text", nullable: false),
                    make = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    seating_capacity = table.Column<int>(type: "integer", nullable: false),
                    licence_plate = table.Column<string>(type: "text", nullable: false),
                    required_licence_class = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    status_reason = table.Column<string>(type: "text", nullable: true),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    acquisition_cost_cad = table.Column<decimal>(type: "numeric", nullable: false),
                    end_of_life_km = table.Column<int>(type: "integer", nullable: false),
                    sale_price_cad = table.Column<decimal>(type: "numeric", nullable: true),
                    disposed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    requires_periodic_inspection = table.Column<bool>(type: "boolean", nullable: false),
                    current_value_cad = table.Column<decimal>(type: "numeric", nullable: false),
                    remaining_km = table.Column<int>(type: "integer", nullable: false),
                    life_used_pct = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_vehicles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_work_orders",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    source_ref = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_to = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    line_items = table.Column<List<string>>(type: "text[]", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolving_service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorized_limit_cad = table.Column<decimal>(type: "numeric", nullable: true),
                    budget_code = table.Column<string>(type: "text", nullable: true),
                    date_required_or_oos = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_work_orders", x => x.id);
                });

            // ---- Tenant isolation, DB half — the same idiom as the write tables and the audit
            // infrastructure (NULLIF guards the pooled-connection case where the session variable
            // is unset). FORCE binds the owner too: migrations run as northernlink_app, which
            // therefore owns these tables, so without FORCE the policy would not apply to the very
            // role the API connects as. No GRANTs are needed for the same reason — the app already
            // owns them. The app.is_system arm is what lets the tenant-less projection worker and
            // rebuilder write across tenants.
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

            // Read models start empty; the projection worker fills them from the journal as
            // aggregates change. Run ProjectionRebuilder to backfill rows for data that already
            // existed before this migration.
        }

        /// <summary>
        /// Drops the projection tables. Deliberately does NOT recreate the materialized views,
        /// the wrapper views, or fleet.round_even: this migration is a one-way architectural
        /// move, and resurrecting that design would also resurrect its dependency on a
        /// hand-provisioned northernlink_projector role. Roll forward, not back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rm_retirement_certificates",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_service_records",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_shops",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_vehicle_documents",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_vehicle_inspections",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_vehicles",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "rm_work_orders",
                schema: "fleet");
        }
    }
}
