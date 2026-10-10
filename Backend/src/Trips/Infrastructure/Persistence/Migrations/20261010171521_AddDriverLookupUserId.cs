using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverLookupUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                schema: "trips",
                table: "driver_lookup",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_driver_lookup_tenant_id_user_id",
                schema: "trips",
                table: "driver_lookup",
                columns: new[] { "tenant_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            // Backfill user_id from the Drivers source of truth — the same reasoning as
            // BackfillBudgetingUserLookup, and like it this one is MANDATORY rather than a
            // self-healing convenience:
            //
            //   driver_lookup is kept current by the DriverChangedIntegrationEvent handler, but
            //   the outbox rows already consumed for existing drivers were written BEFORE the
            //   event carried userId, and the consumer does not replay delivered history. So
            //   every driver linked to an account before this migration would sit with a null
            //   user_id until their roster row is next edited — and until then the account would
            //   be refused on every driver-facing trip route (Trips.Trip.NotYourTrip) with
            //   nothing logging an error.
            //
            // Cross-schema read of drivers.drivers is acceptable here — a migration is shared
            // database infrastructure, not module code, so the no-cross-module rule that binds
            // library code does not apply. Both columns are uuid, so no cast.
            //
            // Guarded on the table existing, for two real cases: the module migration runner
            // walks Trips BEFORE Drivers (Program.cs), so on a fresh database drivers.drivers
            // does not exist yet (and holds nothing to copy); and the Trips integration-test
            // fixture migrates only the fleet + trips schemas. Skipping is correct in both — there
            // are no pre-existing drivers to backfill — and the shared DigitalOcean database, the
            // one place this matters, has the table.
            //
            // RLS: migrations run as the table owner, and FORCE ROW LEVEL SECURITY applies to the
            // owner too. drivers.drivers' policy carries a FOR SELECT app.is_system arm and
            // trips.driver_lookup has driver_lookup_system_access, so the transaction-local
            // set_config (the DO-block form of SET LOCAL, same transaction as the UPDATE — EF runs
            // the migration inside one) lets a single statement read and write every tenant's
            // rows. Without it the UPDATE silently matches zero rows and the migration "succeeds"
            // having done nothing. Idempotent: re-running re-copies the same values.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('drivers.drivers') IS NULL THEN
                        RAISE NOTICE 'drivers.drivers is not present; skipping the driver_lookup.user_id backfill';
                        RETURN;
                    END IF;

                    PERFORM set_config('app.is_system', 'true', true);

                    UPDATE trips.driver_lookup dl
                    SET user_id = d.user_id, updated_at_utc = now()
                    FROM drivers.drivers d
                    WHERE d.id = dl.driver_id
                      AND d.tenant_id = dl.tenant_id
                      AND dl.user_id IS DISTINCT FROM d.user_id;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the column discards the backfilled values with it; nothing else to undo.
            migrationBuilder.DropIndex(
                name: "IX_driver_lookup_tenant_id_user_id",
                schema: "trips",
                table: "driver_lookup");

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "trips",
                table: "driver_lookup");
        }
    }
}
