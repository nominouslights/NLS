using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_lookup",
                schema: "trips",
                columns: table => new
                {
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_lookup", x => x.client_id);
                });

            migrationBuilder.CreateTable(
                name: "driver_lookup",
                schema: "trips",
                columns: table => new
                {
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    licence_class = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_lookup", x => x.driver_id);
                });

            migrationBuilder.CreateTable(
                name: "rm_routes",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    destination = table.Column<string>(type: "text", nullable: false),
                    distance_km = table.Column<int>(type: "integer", nullable: false),
                    estimated_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    required_licence_class = table.Column<string>(type: "text", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    stops = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_routes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_schedule_templates",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_name = table.Column<string>(type: "text", nullable: true),
                    service_type = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "text", nullable: true),
                    days_of_week = table.Column<List<string>>(type: "text[]", nullable: false),
                    departure_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    return_departure_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    seats_capacity = table.Column<int>(type: "integer", nullable: false),
                    seats_minimum = table.Column<int>(type: "integer", nullable: true),
                    default_vehicle_unit = table.Column<string>(type: "text", nullable: true),
                    default_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generation_horizon_days = table.Column<int>(type: "integer", nullable: false),
                    cutoff_note = table.Column<string>(type: "text", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_schedule_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_trips",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_number = table.Column<string>(type: "text", nullable: false),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    window_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    window_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    service_type = table.Column<string>(type: "text", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: true),
                    route_name = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    destination = table.Column<string>(type: "text", nullable: false),
                    distance_km = table.Column<int>(type: "integer", nullable: false),
                    schedule_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    round_trip_key = table.Column<string>(type: "text", nullable: true),
                    direction = table.Column<string>(type: "text", nullable: true),
                    is_empty_leg = table.Column<bool>(type: "boolean", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "text", nullable: true),
                    po_number = table.Column<string>(type: "text", nullable: true),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_name = table.Column<string>(type: "text", nullable: true),
                    vehicle_unit = table.Column<string>(type: "text", nullable: true),
                    seats_capacity = table.Column<int>(type: "integer", nullable: true),
                    seats_confirmed = table.Column<int>(type: "integer", nullable: false),
                    seats_minimum = table.Column<int>(type: "integer", nullable: true),
                    demand_guaranteed = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    manifest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    stops = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_trips", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    distance_km = table.Column<int>(type: "integer", nullable: false),
                    estimated_duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    required_licence_class = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    stops = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_templates",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    days_of_week = table.Column<string>(type: "jsonb", nullable: false),
                    departure_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    return_departure_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    seats_capacity = table.Column<int>(type: "integer", nullable: false),
                    seats_minimum = table.Column<int>(type: "integer", nullable: true),
                    default_vehicle_unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    default_driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generation_horizon_days = table.Column<int>(type: "integer", nullable: false),
                    cutoff_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trip_number_counters",
                schema: "trips",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_number_counters", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    window_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    window_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    service_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: true),
                    route_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origin = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    distance_km = table.Column<int>(type: "integer", nullable: false),
                    schedule_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    round_trip_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    is_empty_leg = table.Column<bool>(type: "boolean", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    po_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    vehicle_unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    seats_capacity = table.Column<int>(type: "integer", nullable: true),
                    seats_confirmed = table.Column<int>(type: "integer", nullable: false),
                    seats_minimum = table.Column<int>(type: "integer", nullable: true),
                    demand_guaranteed = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    manifest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    stops = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_lookup_tenant_id",
                schema: "trips",
                table: "client_lookup",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_driver_lookup_tenant_id_status",
                schema: "trips",
                table: "driver_lookup",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_trips_tenant_id_service_date",
                schema: "trips",
                table: "rm_trips",
                columns: new[] { "tenant_id", "service_date" });

            migrationBuilder.CreateIndex(
                name: "IX_routes_tenant_id_name",
                schema: "trips",
                table: "routes",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_templates_tenant_id_active",
                schema: "trips",
                table: "schedule_templates",
                columns: new[] { "tenant_id", "active" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_templates_tenant_id_route_id",
                schema: "trips",
                table: "schedule_templates",
                columns: new[] { "tenant_id", "route_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_tenant_id_client_id",
                schema: "trips",
                table: "trips",
                columns: new[] { "tenant_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_tenant_id_driver_id",
                schema: "trips",
                table: "trips",
                columns: new[] { "tenant_id", "driver_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_tenant_id_schedule_template_id_service_date_direction",
                schema: "trips",
                table: "trips",
                columns: new[] { "tenant_id", "schedule_template_id", "service_date", "direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trips_tenant_id_service_date",
                schema: "trips",
                table: "trips",
                columns: new[] { "tenant_id", "service_date" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_tenant_id_trip_number",
                schema: "trips",
                table: "trips",
                columns: new[] { "tenant_id", "trip_number" },
                unique: true);

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule, same idioms as
            // the earlier Trips migrations. NULLIF guards the pooled-connection case where the
            // session variable resets to the empty string; FORCE binds the table owner too
            // (migrations run as the app role, which owns these tables).
            //
            // 1. Write tables + replicas + the trip-number counter get the tenant policy PLUS
            //    a separate permissive app.is_system policy (the outbox_messages shape): the
            //    trip generation worker enumerates active templates across tenants on a
            //    pinned system session, and the projection worker reads the write tables the
            //    same way. Request-path connections never set app.is_system, so tenant
            //    isolation holds for them.
            //
            // 2. rm_* projection tables use the single OR-arm policy, exactly like
            //    rm_trip_manifests: the tenant-less projection worker/rebuilder writes them
            //    across tenants.
            migrationBuilder.Sql(
                """
                ALTER TABLE trips.trips ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.trips FORCE ROW LEVEL SECURITY;
                CREATE POLICY trips_tenant_isolation ON trips.trips
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY trips_system_access ON trips.trips
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.routes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.routes FORCE ROW LEVEL SECURITY;
                CREATE POLICY routes_tenant_isolation ON trips.routes
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY routes_system_access ON trips.routes
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.schedule_templates ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.schedule_templates FORCE ROW LEVEL SECURITY;
                CREATE POLICY schedule_templates_tenant_isolation ON trips.schedule_templates
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY schedule_templates_system_access ON trips.schedule_templates
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.driver_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.driver_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY driver_lookup_tenant_isolation ON trips.driver_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY driver_lookup_system_access ON trips.driver_lookup
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.client_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.client_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY client_lookup_tenant_isolation ON trips.client_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY client_lookup_system_access ON trips.client_lookup
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.trip_number_counters ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.trip_number_counters FORCE ROW LEVEL SECURITY;
                CREATE POLICY trip_number_counters_tenant_isolation ON trips.trip_number_counters
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY trip_number_counters_system_access ON trips.trip_number_counters
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.rm_trips ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.rm_trips FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_trips_tenant_isolation ON trips.rm_trips
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.rm_routes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.rm_routes FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_routes_tenant_isolation ON trips.rm_routes
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.rm_schedule_templates ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.rm_schedule_templates FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_schedule_templates_tenant_isolation ON trips.rm_schedule_templates
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_lookup",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "driver_lookup",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "rm_routes",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "rm_schedule_templates",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "rm_trips",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "routes",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "schedule_templates",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "trip_number_counters",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "trips",
                schema: "trips");
        }
    }
}
