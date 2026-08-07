using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripBillingReplica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trip_billing",
                schema: "trips",
                columns: table => new
                {
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    qbo_invoice_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    qbo_entered_date = table.Column<DateOnly>(type: "date", nullable: true),
                    payment_confirmed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_billing", x => x.trip_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trip_billing_tenant_id_invoice_id",
                schema: "trips",
                table: "trip_billing",
                columns: new[] { "tenant_id", "invoice_id" });

            // Tenant isolation is enforced twice: the EF query filter above the API and this
            // policy in the database. EF never generates RLS, so every new tenant-scoped table
            // repeats this block by hand. NULLIF keeps a pooled connection with no tenant set
            // from failing the ::uuid cast; the system policy is for the projection and outbox
            // workers, which run with no ambient tenant.
            migrationBuilder.Sql(
                """
                ALTER TABLE trips.trip_billing ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.trip_billing FORCE ROW LEVEL SECURITY;
                CREATE POLICY trip_billing_tenant_isolation ON trips.trip_billing
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY trip_billing_system_access ON trips.trip_billing
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_billing",
                schema: "trips");
        }
    }
}
