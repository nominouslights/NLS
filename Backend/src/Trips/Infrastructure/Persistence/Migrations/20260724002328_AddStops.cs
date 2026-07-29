using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rm_stops",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true),
                    street = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: false),
                    province = table.Column<string>(type: "text", nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_stops", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stops",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    province = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stops", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rm_stops_tenant_id_active",
                schema: "trips",
                table: "rm_stops",
                columns: new[] { "tenant_id", "active" });

            migrationBuilder.CreateIndex(
                name: "IX_stops_tenant_id_active",
                schema: "trips",
                table: "stops",
                columns: new[] { "tenant_id", "active" });

            migrationBuilder.CreateIndex(
                name: "IX_stops_tenant_id_name",
                schema: "trips",
                table: "stops",
                columns: new[] { "tenant_id", "name" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule, same idioms as
            // the earlier Trips migrations. NULLIF guards the pooled-connection case where the
            // session variable resets to the empty string; FORCE binds the table owner too
            // (migrations run as the app role, which owns these tables).
            //
            // 1. trips.stops is a write table → tenant policy PLUS a separate permissive
            //    app.is_system policy (same shape as trips.routes): the projection worker reads
            //    the write table across tenants on a pinned system session. Request-path
            //    connections never set app.is_system, so tenant isolation holds for them.
            //
            // 2. trips.rm_stops is an rm_* projection table → the single OR-arm policy, exactly
            //    like rm_routes: the tenant-less projection worker/rebuilder writes it across
            //    tenants.
            migrationBuilder.Sql(
                """
                ALTER TABLE trips.stops ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.stops FORCE ROW LEVEL SECURITY;
                CREATE POLICY stops_tenant_isolation ON trips.stops
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY stops_system_access ON trips.stops
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.rm_stops ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.rm_stops FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_stops_tenant_isolation ON trips.rm_stops
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rm_stops",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "stops",
                schema: "trips");
        }
    }
}
