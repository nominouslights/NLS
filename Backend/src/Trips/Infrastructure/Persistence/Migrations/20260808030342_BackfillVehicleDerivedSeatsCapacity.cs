using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillVehicleDerivedSeatsCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trip seats capacity is now derived from the assigned fleet vehicle (snapshotted
            // from trips.vehicle_lookup like the unit number), but trips created before that
            // rule could carry a vehicle_id with a null seats_capacity — the wizard let the
            // capacity field stay blank — which silently skips the overbooking guard in
            // Trip.RecordDemand. This backfill stamps those trips with their vehicle's current
            // capacity from the replica. Only null capacities are touched, so any deliberately
            // recorded manual value survives and re-running is a harmless no-op (idempotent).
            //
            // SET LOCAL app.is_system MUST be in the same batch (same migrationBuilder.Sql call)
            // as the UPDATE: SET LOCAL is scoped to the surrounding transaction, and EF runs the
            // migration inside one. It bypasses trips.trips' FORCE ROW LEVEL SECURITY so the
            // cross-tenant backfill can write every tenant's rows in a single statement.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';
                UPDATE trips.trips t
                SET seats_capacity = vl.seating_capacity
                FROM trips.vehicle_lookup vl
                WHERE t.vehicle_id = vl.vehicle_id AND t.seats_capacity IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op by design. This migration only fills null capacities from the vehicle
            // replica; it cannot know which trips were null before it ran versus which were
            // stamped later by the assignment path, so reversing it would risk nulling out
            // capacities that demand has since been booked against. A backfill is
            // intentionally not reversible.
        }
    }
}
