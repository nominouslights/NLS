using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleLookupAndTripVehicleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "vehicle_id",
                schema: "trips",
                table: "trips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vehicle_id",
                schema: "trips",
                table: "rm_trips",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vehicle_lookup",
                schema: "trips",
                columns: table => new
                {
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    required_licence_class = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    seating_capacity = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_lookup", x => x.vehicle_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_lookup_tenant_id_status",
                schema: "trips",
                table: "vehicle_lookup",
                columns: new[] { "tenant_id", "status" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. trips.vehicle_lookup
            // is a Fleet replica upserted from fleet.vehicle-changed events, so it gets the same
            // canonical block as trips.driver_lookup (20260719094053_AddTripPlanning): the tenant
            // policy PLUS a separate permissive app.is_system policy. The integration handler writes
            // it under the event's tenant pushed as the ambient tenant (request-path connections
            // never set app.is_system, so tenant isolation holds for them). EF does not emit RLS.
            // The new trips.trips.vehicle_id / trips.rm_trips.vehicle_id columns are covered by the
            // policies already on those tables.
            migrationBuilder.Sql(
                """
                ALTER TABLE trips.vehicle_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.vehicle_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY vehicle_lookup_tenant_isolation ON trips.vehicle_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY vehicle_lookup_system_access ON trips.vehicle_lookup
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_lookup",
                schema: "trips");

            migrationBuilder.DropColumn(
                name: "vehicle_id",
                schema: "trips",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "vehicle_id",
                schema: "trips",
                table: "rm_trips");
        }
    }
}
