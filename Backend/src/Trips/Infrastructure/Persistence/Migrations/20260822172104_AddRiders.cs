using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "riders",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rotation_days = table.Column<int>(type: "integer", nullable: true),
                    last_trip_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_trip_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    trip_count = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_riders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_riders",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    service_type = table.Column<string>(type: "text", nullable: false),
                    contact = table.Column<string>(type: "text", nullable: true),
                    rotation_days = table.Column<int>(type: "integer", nullable: true),
                    last_trip_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_trip_number = table.Column<string>(type: "text", nullable: true),
                    trip_count = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_riders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_riders_tenant_id_service_type_normalized_name",
                schema: "trips",
                table: "riders",
                columns: new[] { "tenant_id", "service_type", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_riders_tenant_id_service_type_name",
                schema: "trips",
                table: "rm_riders",
                columns: new[] { "tenant_id", "service_type", "name" });

            // Row-Level Security — hand-written, because EF never generates it. Both tables
            // are tenant-scoped and get the platform's single RLS shape: ENABLE + FORCE + a
            // tenant policy + the system policy the projection and outbox workers run under.
            //
            // The NULLIF(...) is not optional: current_setting returns '' rather than NULL on a
            // pooled connection that has been reset, and casting '' to uuid fails with 22P02 —
            // which breaks every tenant-less session, not just the one that reset.
            //
            // rm_riders carries its own tenant_id and its own policy — read models are plain
            // projector-maintained tables so they carry the same policy as everything else.
            foreach (var table in new[]
            {
                "riders",
                "rm_riders",
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
                name: "riders",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "rm_riders",
                schema: "trips");
        }
    }
}
