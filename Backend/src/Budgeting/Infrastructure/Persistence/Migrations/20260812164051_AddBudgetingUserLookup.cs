using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetingUserLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_lookup",
                schema: "budgeting",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_lookup", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_lookup_tenant_id",
                schema: "budgeting",
                table: "user_lookup",
                column: "tenant_id");

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // EF never generates RLS. This is a replica table, not an aggregate and not an rm_*
            // projection, so it takes the *lookup* policy shape used by trips.client_lookup and
            // trips.vehicle_lookup (see 20260724141500_AddVehicleLookupAndTripVehicleId.cs): two
            // separate policies rather than the single OR'd arm the rm_* tables use.
            //
            //   _tenant_isolation   the request path — a budget-owner picker only ever offers
            //                       users of the caller's own tenant.
            //   _system_access      unrestricted, not SELECT-only: OutboxPollingConsumer runs
            //                       with app.is_system pinned and no ambient tenant, and it has
            //                       to INSERT and UPDATE rows here as user-changed events
            //                       arrive. A read-only bypass would break every upsert.
            //
            // FORCE is mandatory because migrations run as the role that owns the table, and an
            // unforced policy does not bind the owner. The NULLIF guard is mandatory because
            // current_setting returns '' rather than NULL on a pooled connection Npgsql has
            // reset, and casting '' to uuid fails with 22P02 — which breaks every tenant-less
            // session, not just the one that reset.
            migrationBuilder.Sql(
                """
                ALTER TABLE budgeting.user_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.user_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY user_lookup_tenant_isolation ON budgeting.user_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY user_lookup_system_access ON budgeting.user_lookup
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_lookup",
                schema: "budgeting");
        }
    }
}
