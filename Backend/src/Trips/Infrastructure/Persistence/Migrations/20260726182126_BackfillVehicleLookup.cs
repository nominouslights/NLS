using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillVehicleLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase A added trips.vehicle_lookup, kept current by the Fleet
            // VehicleChangedIntegrationEvent handler. Vehicles that predate that wiring never
            // emitted a change event, so they are absent from the replica and POST /api/trips
            // 404s (Trips.Trip.VehicleNotFound) when assigned. This backfill seeds the replica
            // from fleet.vehicles so any fresh deployment self-heals; it is idempotent
            // (ON CONFLICT upsert), so re-running on an already-backfilled DB is a harmless
            // re-upsert. Cross-schema read of fleet.vehicles is acceptable here — a migration is
            // shared database infrastructure, not module code, so the no-cross-module rule that
            // applies to library code does not apply. status is a varchar enum-name string in
            // both tables, so it copies across without a cast.
            //
            // SET LOCAL app.is_system MUST be in the same batch (same migrationBuilder.Sql call)
            // as the INSERT: SET LOCAL is scoped to the surrounding transaction, and EF runs the
            // migration inside one. It bypasses trips.vehicle_lookup's FORCE ROW LEVEL SECURITY
            // (via the vehicle_lookup_system_access policy) so the cross-tenant backfill can
            // write every tenant's rows in a single statement.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';
                INSERT INTO trips.vehicle_lookup (vehicle_id, tenant_id, unit_number, status, required_licence_class, seating_capacity, updated_at_utc)
                SELECT id, tenant_id, unit_number, status, required_licence_class, seating_capacity, now()
                FROM fleet.vehicles
                ON CONFLICT (vehicle_id) DO UPDATE SET
                  unit_number = EXCLUDED.unit_number, status = EXCLUDED.status,
                  required_licence_class = EXCLUDED.required_licence_class,
                  seating_capacity = EXCLUDED.seating_capacity, updated_at_utc = EXCLUDED.updated_at_utc;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op by design. This migration only upserts replica rows into trips.vehicle_lookup
            // from the Fleet source of truth; it never deleted anything, and it cannot know which
            // rows predated it versus which arrived later via the integration-event handler.
            // Reversing it would risk deleting live replica rows that the handler now depends on,
            // so a backfill is intentionally not reversible.
        }
    }
}
