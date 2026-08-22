using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rm_shipment_legs",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_number = table.Column<string>(type: "text", nullable: false),
                    trip_service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_name = table.Column<string>(type: "text", nullable: false),
                    to_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    picked_up_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    picked_up_by = table.Column<string>(type: "text", nullable: true),
                    dropped_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dropped_by = table.Column<string>(type: "text", nullable: true),
                    is_final_leg = table.Column<bool>(type: "boolean", nullable: false),
                    onward_trip_number = table.Column<string>(type: "text", nullable: true),
                    shipment_number = table.Column<string>(type: "text", nullable: false),
                    shipment_status = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "text", nullable: true),
                    consignee_name = table.Column<string>(type: "text", nullable: true),
                    pieces = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    charge_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    hazmat = table.Column<bool>(type: "boolean", nullable: false),
                    secured = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_shipment_legs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_shipments",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_number = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "text", nullable: true),
                    po_number = table.Column<string>(type: "text", nullable: true),
                    consignor_name = table.Column<string>(type: "text", nullable: true),
                    consignor_contact = table.Column<string>(type: "text", nullable: true),
                    consignee_name = table.Column<string>(type: "text", nullable: true),
                    consignee_contact = table.Column<string>(type: "text", nullable: true),
                    origin_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin_name = table.Column<string>(type: "text", nullable: false),
                    destination_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destination_name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    pieces = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    length_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    width_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    hazmat = table.Column<bool>(type: "boolean", nullable: false),
                    declared_value_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    special_handling = table.Column<string>(type: "text", nullable: true),
                    secured = table.Column<bool>(type: "boolean", nullable: false),
                    charge_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    payment_method = table.Column<string>(type: "text", nullable: true),
                    payment_collected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ready_date = table.Column<DateOnly>(type: "date", nullable: true),
                    required_by_date = table.Column<DateOnly>(type: "date", nullable: true),
                    leg_count = table.Column<int>(type: "integer", nullable: false),
                    current_trip_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_trip_number = table.Column<string>(type: "text", nullable: true),
                    last_service_date = table.Column<DateOnly>(type: "date", nullable: true),
                    awaiting_transfer = table.Column<bool>(type: "boolean", nullable: false),
                    delivered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_by = table.Column<string>(type: "text", nullable: true),
                    delivery_note = table.Column<string>(type: "text", nullable: true),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    written_off_reason = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    entered_by = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_shipments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_number_counters",
                schema: "trips",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_number_counters", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    po_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    consignor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    consignor_contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    consignee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    consignee_contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    origin_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destination_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    pieces = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    length_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    width_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    hazmat = table.Column<bool>(type: "boolean", nullable: false),
                    declared_value_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    special_handling = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    secured = table.Column<bool>(type: "boolean", nullable: false),
                    charge_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    payment_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    payment_collected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ready_date = table.Column<DateOnly>(type: "date", nullable: true),
                    required_by_date = table.Column<DateOnly>(type: "date", nullable: true),
                    delivered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    delivery_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancelled_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    written_off_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    entered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_legs",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    trip_service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    to_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    picked_up_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    picked_up_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dropped_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dropped_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_legs", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_legs_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalSchema: "trips",
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rm_shipment_legs_shipment_id_sequence",
                schema: "trips",
                table: "rm_shipment_legs",
                columns: new[] { "shipment_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_shipment_legs_tenant_id_trip_id",
                schema: "trips",
                table: "rm_shipment_legs",
                columns: new[] { "tenant_id", "trip_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_shipment_legs_tenant_id_trip_number",
                schema: "trips",
                table: "rm_shipment_legs",
                columns: new[] { "tenant_id", "trip_number" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_shipments_tenant_id_client_id_status",
                schema: "trips",
                table: "rm_shipments",
                columns: new[] { "tenant_id", "client_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_shipments_tenant_id_shipment_number",
                schema: "trips",
                table: "rm_shipments",
                columns: new[] { "tenant_id", "shipment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_shipments_tenant_id_status",
                schema: "trips",
                table: "rm_shipments",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_legs_shipment_id_sequence",
                schema: "trips",
                table: "shipment_legs",
                columns: new[] { "shipment_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_legs_tenant_id_trip_id",
                schema: "trips",
                table: "shipment_legs",
                columns: new[] { "tenant_id", "trip_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_legs_tenant_id_trip_number",
                schema: "trips",
                table: "shipment_legs",
                columns: new[] { "tenant_id", "trip_number" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_tenant_id_client_id",
                schema: "trips",
                table: "shipments",
                columns: new[] { "tenant_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_tenant_id_shipment_number",
                schema: "trips",
                table: "shipments",
                columns: new[] { "tenant_id", "shipment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_tenant_id_status",
                schema: "trips",
                table: "shipments",
                columns: new[] { "tenant_id", "status" });

            // Row-Level Security — hand-written, because EF never generates it. Every one of
            // these five tables is tenant-scoped and gets the platform's single RLS shape:
            // ENABLE + FORCE + a tenant policy + the system policy the projection and outbox
            // workers run under.
            //
            // The NULLIF(...) is not optional: current_setting returns '' rather than NULL on a
            // pooled connection that has been reset, and casting '' to uuid fails with 22P02 —
            // which breaks every tenant-less session, not just the one that reset.
            //
            // shipment_legs carries its own tenant_id and its own policy rather than inheriting
            // isolation through its foreign key to shipments. Protection-by-association is
            // exactly what the convention forbids.
            foreach (var table in new[]
            {
                "shipments",
                "shipment_legs",
                "shipment_number_counters",
                "rm_shipments",
                "rm_shipment_legs",
            })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE trips.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE trips.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY {table}_tenant_isolation ON trips.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                    CREATE POLICY {table}_system_access ON trips.{table}
                        USING (current_setting('app.is_system', true) = 'true');
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rm_shipment_legs",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "rm_shipments",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "shipment_legs",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "shipment_number_counters",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "shipments",
                schema: "trips");
        }
    }
}
