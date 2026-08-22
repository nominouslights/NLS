using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillBudgetingUserLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddBudgetingUserLookup added budgeting.user_lookup, kept current by the Identity
            // UserChangedIntegrationEvent handler. This backfill is MANDATORY, not a self-healing
            // convenience like BackfillVehicleLookup, and the reason is worth stating plainly:
            //
            //   Identity had no IIntegrationEventMapper at all until the story that added this
            //   table, so identity.outbox_messages has been empty since the schema was created.
            //   AddOutboxPollingConsumer normally replays a routing key's entire outbox history
            //   on first poll — which is exactly how a replica bootstraps itself — but here
            //   there is no history to replay. Without this INSERT, no user who existed before
            //   today would EVER appear in the replica, and the budget-owner picker would be
            //   permanently empty with nothing logging an error.
            //
            // Verified before writing: identity.users carried exactly one row (the Owner account)
            // and identity.outbox_messages was empty.
            //
            // Cross-schema read of identity.users is acceptable here — a migration is shared
            // database infrastructure, not module code, so the no-cross-module rule that binds
            // library code does not apply. email and role are varchars in both tables, so they
            // copy across without a cast.
            //
            // SET LOCAL app.is_system MUST be in the same batch (the same migrationBuilder.Sql
            // call) as the INSERT: SET LOCAL is scoped to the surrounding transaction, and EF
            // runs the migration inside one. It is needed on both sides — identity.users'
            // policy carries an `OR app.is_system` arm so the SELECT returns every tenant's
            // rows, and budgeting.user_lookup's user_lookup_system_access policy admits the
            // cross-tenant write. Without it the SELECT silently returns zero rows and the
            // migration "succeeds" having done nothing.
            //
            // Idempotent (ON CONFLICT upsert), so re-running on an already-backfilled database
            // is a harmless re-upsert.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';
                INSERT INTO budgeting.user_lookup (user_id, tenant_id, email, role, updated_at_utc)
                SELECT id, tenant_id, email, role, now()
                FROM identity.users
                ON CONFLICT (user_id) DO UPDATE SET
                  email = EXCLUDED.email, role = EXCLUDED.role,
                  updated_at_utc = EXCLUDED.updated_at_utc;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op by design. This migration only upserts replica rows into
            // budgeting.user_lookup from the Identity source of truth; it never deleted
            // anything, and it cannot know which rows predated it versus which arrived later via
            // the integration-event handler. Reversing it would risk deleting live replica rows
            // the handler now depends on, so a backfill is intentionally not reversible.
        }
    }
}
