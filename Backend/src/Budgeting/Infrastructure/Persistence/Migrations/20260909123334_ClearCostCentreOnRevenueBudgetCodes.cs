using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClearCostCentreOnRevenueBudgetCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only — no schema change, so the model snapshot is untouched and this migration
            // was generated empty on purpose.
            //
            // BudgetCode.Validate now rejects a cost centre on a Revenue code
            // (BudgetCodeErrors.CostCentreNotAllowedForRevenue). Adding an invariant while leaving
            // rows that break it is the thing to avoid: a legacy revenue code would keep rendering
            // a bogus cost centre as fact, and could not be saved from the console until someone
            // noticed. This clears them once, everywhere.
            //
            // `category` is stored HasConversion<string>() into varchar(16) on both tables
            // (BudgetCodeConfiguration, BudgetCodeReadModel), so the string literal is the right
            // comparison — not an ordinal.
            //
            // BOTH tables matter. budgeting.rm_budget_codes is a projector-maintained read model,
            // and the projection only runs on a write: fixing the aggregate table alone would
            // leave the list and detail panel showing the stale value until someone edited each
            // code by hand.
            //
            // RLS is the whole difficulty here, and getting it wrong is silent. A migration runs
            // with no app.tenant_id set, so both tenant policies evaluate
            // `tenant_id = NULLIF('', '')::uuid` → NULL → false, and a blind UPDATE matches zero
            // rows while the migration reports success. BackfillBudgetingUserLookup documents the
            // same trap in its own comment; this is that trap on the write path.
            //
            // Two different obstacles, so two different remedies, both in ONE Sql batch because
            // SET LOCAL is scoped to the surrounding transaction and EF wraps the migration in one:
            //
            //   rm_budget_codes  its policy already carries an `OR app.is_system` arm covering
            //                    every command, so SET LOCAL app.is_system is enough.
            //   budget_codes     its only system policy (budget_codes_system_read) is FOR SELECT,
            //                    so app.is_system does NOT admit an UPDATE. FORCE is dropped for
            //                    the duration instead: the app role owns the table, and an
            //                    unforced policy does not bind the owner. FORCE goes back on in
            //                    the same transaction, so a failure anywhere rolls the whole thing
            //                    back — ALTER TABLE is transactional in Postgres — and the table
            //                    is never left unforced. No policy is created, altered or dropped.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';

                ALTER TABLE budgeting.budget_codes NO FORCE ROW LEVEL SECURITY;
                UPDATE budgeting.budget_codes SET cost_centre = NULL WHERE category = 'Revenue';
                ALTER TABLE budgeting.budget_codes FORCE ROW LEVEL SECURITY;

                UPDATE budgeting.rm_budget_codes SET cost_centre = NULL WHERE category = 'Revenue';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op by design. The cleared cost centres are not recoverable: the UPDATE above
            // overwrote them in place and nothing anywhere retains the old values. Reverting the
            // domain rule brings back the ability to enter a cost centre on a revenue code; it
            // cannot bring back the ones that were wrong.
        }
    }
}
