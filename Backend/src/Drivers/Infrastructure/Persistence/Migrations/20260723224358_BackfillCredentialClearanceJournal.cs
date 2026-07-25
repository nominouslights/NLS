using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only repair: DriverCredential and DriverClearance historically raised no domain
    /// events, so their writes produced no <c>event_journal</c> rows and the projection worker
    /// (which polls the journal) never populated rm_driver_credentials / rm_driver_clearances.
    /// The aggregates now raise events; this migration writes synthetic journal rows for the
    /// pre-existing aggregates so the worker projects them through the normal path (which also
    /// refreshes the stale rm_drivers credential_count / soonest_credential_expiry rollups).
    ///
    /// RLS wrinkle: event_journal's INSERT policy is tenant-scoped with no system bypass, so
    /// the inserts loop per tenant with <c>app.tenant_id</c> pinned to that tenant. Idempotent
    /// via NOT EXISTS — safe on databases where the events fix already journaled these rows.
    /// </summary>
    public partial class BackfillCredentialClearanceJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SELECT set_config('app.is_system', 'true', false);

                DO $$
                DECLARE t uuid;
                BEGIN
                  FOR t IN SELECT DISTINCT tenant_id FROM drivers.driver_credentials
                           UNION SELECT DISTINCT tenant_id FROM drivers.driver_clearances LOOP
                    PERFORM set_config('app.tenant_id', t::text, false);

                    INSERT INTO drivers.event_journal
                      (event_id, tenant_id, aggregate_type, aggregate_id, aggregate_version,
                       event_type, payload, occurred_at_utc, recorded_at_utc)
                    SELECT gen_random_uuid(), c.tenant_id, 'driver-credential', c.id, c.version,
                           'driver-credential-added', '{}'::jsonb, c.created_at_utc, now()
                    FROM drivers.driver_credentials c
                    WHERE c.tenant_id = t
                      AND NOT EXISTS (SELECT 1 FROM drivers.event_journal j
                                      WHERE j.aggregate_id = c.id AND j.aggregate_type = 'driver-credential');

                    INSERT INTO drivers.event_journal
                      (event_id, tenant_id, aggregate_type, aggregate_id, aggregate_version,
                       event_type, payload, occurred_at_utc, recorded_at_utc)
                    SELECT gen_random_uuid(), cl.tenant_id, 'driver-clearance', cl.id, cl.version,
                           'driver-clearance-granted', '{}'::jsonb, cl.granted_at_utc, now()
                    FROM drivers.driver_clearances cl
                    WHERE cl.tenant_id = t
                      AND NOT EXISTS (SELECT 1 FROM drivers.event_journal j
                                      WHERE j.aggregate_id = cl.id AND j.aggregate_type = 'driver-clearance');
                  END LOOP;

                  PERFORM set_config('app.tenant_id', '', false);
                  PERFORM set_config('app.is_system', '', false);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only repair; the synthetic journal rows are harmless history. No rollback.
        }
    }
}
