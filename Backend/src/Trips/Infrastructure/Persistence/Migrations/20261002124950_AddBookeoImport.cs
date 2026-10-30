using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookeoImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "import_source",
                schema: "trips",
                table: "trips",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bookeo_bookings",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    product_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    window_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    window_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    bookeo_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    participants = table.Column<int>(type: "integer", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    customer_phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    total_gross_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_paid_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_due_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit_text = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    first_imported_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_imported_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passengers = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookeo_bookings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookeo_import_batches",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    uploaded_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    parsed_rows = table.Column<string>(type: "jsonb", nullable: false),
                    plan_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    summary = table.Column<string>(type: "jsonb", nullable: false),
                    committed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    committed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookeo_import_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookeo_product_mappings",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    resident_stop_role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookeo_product_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookeo_unit_mappings",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_text = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookeo_unit_mappings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookeo_bookings_tenant_id_booking_number",
                schema: "trips",
                table: "bookeo_bookings",
                columns: new[] { "tenant_id", "booking_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookeo_bookings_tenant_id_trip_id",
                schema: "trips",
                table: "bookeo_bookings",
                columns: new[] { "tenant_id", "trip_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bookeo_import_batches_tenant_id_uploaded_at_utc",
                schema: "trips",
                table: "bookeo_import_batches",
                columns: new[] { "tenant_id", "uploaded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_bookeo_product_mappings_tenant_id_product_code",
                schema: "trips",
                table: "bookeo_product_mappings",
                columns: new[] { "tenant_id", "product_code" });

            migrationBuilder.CreateIndex(
                name: "IX_bookeo_unit_mappings_tenant_id_unit_text",
                schema: "trips",
                table: "bookeo_unit_mappings",
                columns: new[] { "tenant_id", "unit_text" },
                unique: true);

            // ---- Hand-appended ----
            // 1. Product-mapping uniqueness: one mapping per (tenant, product code, destination),
            //    where a null destination ("any") is its own key and destinations compare
            //    case-insensitively — an expression index EF cannot model. The upsert catches the
            //    23505 a concurrent save trips over.
            // 2. Postgres Row-Level Security (dual tenant enforcement), the canonical shape copied
            //    from AddScheduleExceptions: ENABLE + FORCE + a native tenant policy on each plain
            //    table. No app.is_system arm: only the request path ever reads or writes these
            //    tables (no worker, no projection). NULLIF guards the pooled-connection
            //    empty-string case; FORCE binds the table owner too.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_bookeo_product_mappings_tenant_code_destination"
                    ON trips.bookeo_product_mappings (tenant_id, product_code, lower(coalesce(destination, '')));

                ALTER TABLE trips.bookeo_import_batches ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.bookeo_import_batches FORCE ROW LEVEL SECURITY;
                CREATE POLICY bookeo_import_batches_tenant_isolation ON trips.bookeo_import_batches
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE trips.bookeo_bookings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.bookeo_bookings FORCE ROW LEVEL SECURITY;
                CREATE POLICY bookeo_bookings_tenant_isolation ON trips.bookeo_bookings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE trips.bookeo_product_mappings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.bookeo_product_mappings FORCE ROW LEVEL SECURITY;
                CREATE POLICY bookeo_product_mappings_tenant_isolation ON trips.bookeo_product_mappings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE trips.bookeo_unit_mappings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.bookeo_unit_mappings FORCE ROW LEVEL SECURITY;
                CREATE POLICY bookeo_unit_mappings_tenant_isolation ON trips.bookeo_unit_mappings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookeo_bookings",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "bookeo_import_batches",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "bookeo_product_mappings",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "bookeo_unit_mappings",
                schema: "trips");

            migrationBuilder.DropColumn(
                name: "import_source",
                schema: "trips",
                table: "trips");
        }
    }
}
