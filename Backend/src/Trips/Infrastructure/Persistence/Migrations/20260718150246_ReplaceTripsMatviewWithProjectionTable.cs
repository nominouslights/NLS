using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Trips' half of the read-side move from materialized views to projector-maintained tables —
    /// see the Fleet migration ReplaceFleetMatviewsWithProjectionTables for the full rationale.
    /// Both modules had to change together: the projector role is removed from provisioning, so
    /// any migration still granting to it would fail with 42704 on a fresh database.
    /// </summary>
    public partial class ReplaceTripsMatviewWithProjectionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Wrapper view first — it depends on the matview. Dropping both also drops every
            // grant and ownership tie to northernlink_projector.
            migrationBuilder.Sql(
                """
                DROP VIEW IF EXISTS trips.v_trip_manifests;
                DROP MATERIALIZED VIEW IF EXISTS trips.mv_trip_manifests;
                """);

            migrationBuilder.CreateTable(
                name: "rm_trip_manifests",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_date = table.Column<DateOnly>(type: "date", nullable: false),
                    trip_number = table.Column<string>(type: "text", nullable: false),
                    route = table.Column<string>(type: "text", nullable: false),
                    direction = table.Column<string>(type: "text", nullable: true),
                    client = table.Column<string>(type: "text", nullable: true),
                    unit = table.Column<string>(type: "text", nullable: false),
                    driver_name = table.Column<string>(type: "text", nullable: false),
                    driver_licence_no = table.Column<string>(type: "text", nullable: true),
                    licence_plate = table.Column<string>(type: "text", nullable: true),
                    odometer_start_km = table.Column<int>(type: "integer", nullable: true),
                    fuel_level = table.Column<string>(type: "text", nullable: true),
                    weather = table.Column<List<string>>(type: "text[]", nullable: false),
                    temperature_c = table.Column<string>(type: "text", nullable: true),
                    road_conditions = table.Column<List<string>>(type: "text[]", nullable: false),
                    visibility = table.Column<string>(type: "text", nullable: true),
                    road_advisories = table.Column<string>(type: "text", nullable: true),
                    all_seatbelts_verified = table.Column<bool>(type: "boolean", nullable: false),
                    all_cargo_secured = table.Column<string>(type: "text", nullable: true),
                    issues = table.Column<List<string>>(type: "text[]", nullable: false),
                    no_issues = table.Column<bool>(type: "boolean", nullable: false),
                    departure_time = table.Column<string>(type: "text", nullable: true),
                    arrival_time = table.Column<string>(type: "text", nullable: true),
                    odometer_end_km = table.Column<int>(type: "integer", nullable: true),
                    total_km = table.Column<int>(type: "integer", nullable: true),
                    fuel_added = table.Column<bool>(type: "boolean", nullable: false),
                    fuel_litres = table.Column<decimal>(type: "numeric", nullable: true),
                    fuel_cost_cad = table.Column<decimal>(type: "numeric", nullable: true),
                    attestations = table.Column<List<bool>>(type: "boolean[]", nullable: false),
                    driver_signature_name = table.Column<string>(type: "text", nullable: false),
                    certified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    entered_by = table.Column<string>(type: "text", nullable: true),
                    entered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    cargo = table.Column<string>(type: "jsonb", nullable: true),
                    passengers = table.Column<string>(type: "jsonb", nullable: true),
                    post_trip_items = table.Column<string>(type: "jsonb", nullable: true),
                    pre_trip_items = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_trip_manifests", x => x.id);
                });

            // Tenant isolation, DB half — the same native RLS idiom as the write tables (NULLIF
            // guards the pooled-connection case where the session variable is unset). FORCE binds
            // the owner too: migrations run as northernlink_app, which owns this table, so without
            // FORCE the policy would not apply to the role the API connects as. No GRANTs needed
            // for the same reason. The app.is_system arm lets the tenant-less projection worker
            // and rebuilder write across tenants.
            migrationBuilder.Sql(
                """
                ALTER TABLE trips.rm_trip_manifests ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.rm_trip_manifests FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_trip_manifests_tenant_isolation ON trips.rm_trip_manifests
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);

            // Read model starts empty; the worker fills it from the journal as manifests change.
            // Run ProjectionRebuilder to backfill rows for data that predates this migration.
        }

        /// <inheritdoc />
        /// <summary>
        /// Drops the projection table. Deliberately does NOT recreate the materialized view or
        /// its wrapper: this is a one-way architectural move, and restoring that design would
        /// restore its dependency on a hand-provisioned northernlink_projector role.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rm_trip_manifests",
                schema: "trips");
        }
    }
}
