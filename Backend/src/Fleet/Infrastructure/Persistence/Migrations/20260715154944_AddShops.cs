using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shops",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    gst_business_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mpi_accredited = table.Column<bool>(type: "boolean", nullable: false),
                    inspection_station_no = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    supplies_parts = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shops", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shops_tenant_id_number",
                schema: "fleet",
                table: "shops",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // Same pattern as the earlier Fleet migrations, using the NULLIF guard from
            // AddFleetAuditInfrastructure (Npgsql's pool reset turns an unset GUC into the
            // empty string; NULLIF turns that back into NULL so the policy cleanly matches
            // no rows instead of throwing 22P02). Shops are tenant-scoped reference data,
            // so they get the standard tenant isolation policy.
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.shops ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.shops FORCE ROW LEVEL SECURITY;
                CREATE POLICY shops_tenant_isolation ON fleet.shops
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shops",
                schema: "fleet");
        }
    }
}
