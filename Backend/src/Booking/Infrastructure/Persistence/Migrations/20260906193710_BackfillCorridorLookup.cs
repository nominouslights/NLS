using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCorridorLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // InitialBookingSchema added booking.corridor_lookup, kept current by the Trips
            // RouteChangedIntegrationEvent handler. Routes that predate that wiring never
            // emitted a change event, so they are absent from the replica and every corridor
            // API (POST /api/booking/bookings, GET /api/booking/corridors) sees an empty
            // roster until each route is re-saved by hand. This backfill seeds the replica
            // so any fresh deployment self-heals; it is idempotent (ON CONFLICT upsert), so
            // re-running on an already-backfilled DB is a harmless re-upsert. The source is
            // trips.rm_routes — NOT trips.routes — because origin/destination are persisted
            // columns on the read model while the write table derives them from the stops
            // jsonb. Cross-schema read of trips.rm_routes is acceptable here — a migration is
            // shared database infrastructure, not module code, so the no-cross-module rule
            // that applies to library code does not apply.
            //
            // SET LOCAL app.is_system MUST be in the same batch (same migrationBuilder.Sql
            // call) as the INSERT: SET LOCAL is scoped to the surrounding transaction, and EF
            // runs the migration inside one. It bypasses FORCE ROW LEVEL SECURITY on both
            // sides (trips.rm_routes' policy carries an app.is_system arm, and
            // booking.corridor_lookup has its own system policy) so the cross-tenant backfill
            // can copy every tenant's rows in a single statement.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';
                INSERT INTO booking.corridor_lookup (corridor_id, tenant_id, name, origin, destination, active, updated_at_utc)
                SELECT id, tenant_id, name, origin, destination, active, now()
                FROM trips.rm_routes
                ON CONFLICT (corridor_id) DO UPDATE SET
                  name = EXCLUDED.name, origin = EXCLUDED.origin,
                  destination = EXCLUDED.destination, active = EXCLUDED.active,
                  updated_at_utc = EXCLUDED.updated_at_utc;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op by design. This migration only upserts replica rows into
            // booking.corridor_lookup from the Trips source of truth; it never deleted
            // anything, and it cannot know which rows predated it versus which arrived later
            // via the integration-event handler. Reversing it would risk deleting live
            // replica rows that the handler now depends on, so a backfill is intentionally
            // not reversible.
        }
    }
}
