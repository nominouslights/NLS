using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsUserLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_lookup",
                schema: "notifications",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    full_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_lookup", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_lookup_tenant_id",
                schema: "notifications",
                table: "user_lookup",
                column: "tenant_id");

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // EF never generates RLS. This is a replica table, not an aggregate and not an rm_*
            // projection, so it takes the *lookup* policy shape of budgeting.user_lookup
            // (20260812164051_AddBudgetingUserLookup.cs): two separate policies rather than the
            // single OR'd arm the rm_* tables use.
            //
            //   _tenant_isolation   the request path — nothing reads this table from a request
            //                       today, but the policy is the floor every tenant-scoped
            //                       table stands on, present before anything needs it.
            //   _system_access      unrestricted, not SELECT-only: OutboxPollingConsumer runs
            //                       with app.is_system pinned and no ambient tenant, and it has
            //                       to INSERT and UPDATE rows here as user-changed events
            //                       arrive, and SELECT the alert audience from the Drivers
            //                       handlers. A read-only bypass would break every upsert.
            //
            // FORCE is mandatory because migrations run as the role that owns the table, and an
            // unforced policy does not bind the owner. The NULLIF guard is mandatory because
            // current_setting returns '' rather than NULL on a pooled connection Npgsql has
            // reset, and casting '' to uuid fails with 22P02 — which breaks every tenant-less
            // session, not just the one that reset.
            migrationBuilder.Sql(
                """
                ALTER TABLE notifications.user_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE notifications.user_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY user_lookup_tenant_isolation ON notifications.user_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY user_lookup_system_access ON notifications.user_lookup
                    USING (current_setting('app.is_system', true) = 'true');
                """);

            // ---- Hand-appended: MANDATORY backfill from identity.users ----
            // The replica is kept current by the UserChangedIntegrationEvent handler, and
            // subscribing normally replays the routing key's whole outbox history on first poll
            // — which is how a replica bootstraps itself. Not here, for a reason worth stating
            // plainly: processing_status is ONE column per producer outbox row, shared by every
            // consuming module, and Budgeting has consumed identity.user-changed since 2026-08.
            // Every existing row is therefore already 'Processed' and will never be offered to
            // this module's handler (from now on a new row fans out to both modules' handlers in
            // one delivery — OutboxPollingConsumer resolves every registered handler for the
            // event type — but history does not come back). Without this INSERT no account that
            // exists today would EVER appear here, and the compliance alerts would go to nobody
            // with nothing logging an error — the same gap BackfillBudgetingUserLookup
            // (20260812164652) closed for Budgeting, for a different cause. Same fix, same shape.
            //
            // Cross-schema read of identity.users is acceptable here — a migration is shared
            // database infrastructure, not module code, so the no-cross-module rule that binds
            // library code does not apply. email, full_name and role are varchars of the same
            // widths in both tables, so they copy across without a cast. is_active is inserted
            // true: Identity has no deactivation yet (see UserLookup.IsActive).
            //
            // Guarded on the table existing, as the Trips driver_lookup backfill
            // (20261010171521_AddDriverLookupUserId.cs) is: an integration-test fixture that
            // migrates only the notifications schema has no identity.users and nothing to copy,
            // and the shared DigitalOcean database — the one place this matters — has it.
            //
            // RLS: migrations run as the table owner, and FORCE applies to the owner too.
            // identity.users' policy carries an app.is_system arm and user_lookup_system_access
            // admits the cross-tenant write, so the transaction-local set_config (the DO-block
            // form of SET LOCAL, same transaction as the INSERT — EF runs the migration inside
            // one) lets a single statement read and write every tenant's rows. Without it the
            // SELECT silently returns zero rows and the migration "succeeds" having done nothing.
            //
            // Idempotent (ON CONFLICT upsert), so re-running on an already-backfilled database
            // is a harmless re-upsert.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('identity.users') IS NULL THEN
                        RAISE NOTICE 'identity.users is not present; skipping the notifications.user_lookup backfill';
                        RETURN;
                    END IF;

                    PERFORM set_config('app.is_system', 'true', true);

                    INSERT INTO notifications.user_lookup (user_id, tenant_id, email, full_name, role, is_active, updated_at_utc)
                    SELECT id, tenant_id, email, full_name, role, true, now()
                    FROM identity.users
                    ON CONFLICT (user_id) DO UPDATE SET
                      email = EXCLUDED.email,
                      full_name = EXCLUDED.full_name,
                      role = EXCLUDED.role,
                      is_active = EXCLUDED.is_active,
                      updated_at_utc = EXCLUDED.updated_at_utc;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table takes its policies and the backfilled rows with it. The
            // backfill itself has no separate undo: it only ever upserted replica rows, and it
            // cannot tell which predated it versus which the handler wrote since.
            migrationBuilder.DropTable(
                name: "user_lookup",
                schema: "notifications");
        }
    }
}
