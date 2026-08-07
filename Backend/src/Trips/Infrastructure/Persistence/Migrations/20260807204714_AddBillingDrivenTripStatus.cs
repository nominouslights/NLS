using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The billing-driven lifecycle's data migration. Two halves:
    /// <para>
    /// Columns — <c>operations_finished_at_utc</c> ("when did the run end", split off
    /// <c>completed_at_utc</c>, which now means "when did the money arrive") and
    /// <c>written_off_reason</c> on the write model, read model, and billing replica.
    /// </para>
    /// <para>
    /// Reclassification — existing Completed client trips were completed under the old meaning
    /// (run over), so each is re-filed where the new lifecycle says it belongs, derived from
    /// <c>trip_billing</c>: Invoiced-claimed → Invoiced, Paid → stays Completed, everything else
    /// (unclaimed, or only on a draft worksheet) → ReadyForBilling. Clientless trips keep
    /// Completed — they never enter the billing arc. <c>version</c> is never touched (it is the
    /// concurrency token), and <c>rm_trips</c> is backfilled in one reconciling statement because
    /// none of this writes event-journal rows, so the projector would never re-run.
    /// </para>
    /// <para>
    /// PRE-FLIGHT (deploy runbook): drain the outbox first — the reclassification trusts
    /// <c>trip_billing</c>, which is poller-maintained, so
    /// <c>SELECT count(*) FROM billing.outbox_messages WHERE routing_key =
    /// 'billing.invoice-billing-state-changed' AND processing_status = 'Pending'</c> and the same
    /// for <c>trips.trip-completed</c> must both be 0, then snapshot. A trip mis-filed as
    /// ReadyForBilling because its Invoiced event was still Pending would never self-correct.
    /// </para>
    /// </summary>
    public partial class AddBillingDrivenTripStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "operations_finished_at_utc",
                schema: "trips",
                table: "trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "written_off_reason",
                schema: "trips",
                table: "trips",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "written_off_reason",
                schema: "trips",
                table: "trip_billing",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "operations_finished_at_utc",
                schema: "trips",
                table: "rm_trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "written_off_reason",
                schema: "trips",
                table: "rm_trips",
                type: "text",
                nullable: true);

            // The old completion time IS the operational finish time — under the old lifecycle
            // a trip completed the moment its run ended.
            migrationBuilder.Sql(
                """
                UPDATE trips.trips
                   SET operations_finished_at_utc = completed_at_utc
                 WHERE completed_at_utc IS NOT NULL;
                """);

            // Reclassify Completed client trips. This runs FIRST: the ReadyForBilling sweep
            // below keys on "still Completed", so Invoiced rows must be moved out of its way.
            migrationBuilder.Sql(
                """
                UPDATE trips.trips t
                   SET status = 'Invoiced',
                       completed_at_utc = NULL,
                       updated_at_utc = now()
                  FROM trips.trip_billing b
                 WHERE b.trip_id = t.id
                   AND b.tenant_id = t.tenant_id
                   AND t.status = 'Completed'
                   AND t.client_id IS NOT NULL
                   AND b.state = 'Invoiced';
                """);

            // Paid-claimed trips stay Completed — under the new meaning too, their money arrived.
            // Stated for the record; deliberately no statement.

            // Every other completed client trip — unclaimed, or claimed only by a draft
            // ('OnWorksheet' is a draft, not an invoice) — is still waiting on billing.
            migrationBuilder.Sql(
                """
                UPDATE trips.trips t
                   SET status = 'ReadyForBilling',
                       completed_at_utc = NULL,
                       updated_at_utc = now()
                 WHERE t.status = 'Completed'
                   AND t.client_id IS NOT NULL
                   AND NOT EXISTS (SELECT 1
                                     FROM trips.trip_billing b
                                    WHERE b.trip_id = t.id
                                      AND b.tenant_id = t.tenant_id
                                      AND b.state IN ('Invoiced', 'Paid'));
                """);

            // rm_trips backfill. None of the statements above write event-journal rows, so the
            // projector never re-runs for these trips — without this the read model would stay
            // on the old statuses forever.
            migrationBuilder.Sql(
                """
                UPDATE trips.rm_trips r
                   SET status = t.status,
                       completed_at_utc = t.completed_at_utc,
                       operations_finished_at_utc = t.operations_finished_at_utc,
                       written_off_reason = t.written_off_reason,
                       updated_at_utc = t.updated_at_utc
                  FROM trips.trips t
                 WHERE t.id = r.id;
                """);
        }

        /// <summary>
        /// Lossy but runnable: the new statuses collapse back to the old vocabulary
        /// (ReadyForBilling/Invoiced → Completed, WrittenOff → Cancelled as the nearest
        /// old-world terminal), completion time is restored from the operational finish time,
        /// and only then are the columns dropped.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE trips.trips
                   SET status = CASE status
                                    WHEN 'ReadyForBilling' THEN 'Completed'
                                    WHEN 'Invoiced' THEN 'Completed'
                                    WHEN 'WrittenOff' THEN 'Cancelled'
                                    ELSE status
                                END,
                       completed_at_utc = CASE
                                              WHEN status IN ('ReadyForBilling', 'Invoiced')
                                                  THEN COALESCE(completed_at_utc, operations_finished_at_utc)
                                              ELSE completed_at_utc
                                          END
                 WHERE status IN ('ReadyForBilling', 'Invoiced', 'WrittenOff');

                UPDATE trips.rm_trips r
                   SET status = t.status,
                       completed_at_utc = t.completed_at_utc
                  FROM trips.trips t
                 WHERE t.id = r.id;
                """);

            migrationBuilder.DropColumn(
                name: "operations_finished_at_utc",
                schema: "trips",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "written_off_reason",
                schema: "trips",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "written_off_reason",
                schema: "trips",
                table: "trip_billing");

            migrationBuilder.DropColumn(
                name: "operations_finished_at_utc",
                schema: "trips",
                table: "rm_trips");

            migrationBuilder.DropColumn(
                name: "written_off_reason",
                schema: "trips",
                table: "rm_trips");
        }
    }
}
