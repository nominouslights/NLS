using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLookupFullName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "full_name",
                schema: "budgeting",
                table: "user_lookup",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Hand-appended backfill, modelled on BackfillBudgetingUserLookup.
            //
            // Unlike that one this is NOT mandatory, and on a normal deployed startup it is a
            // provable no-op: ModuleMigrationRunner applies Identity's schema before Budgeting's
            // under one advisory lock, so identity.users.full_name is brand new and null for
            // every row when this runs. It is insurance for two real cases that do happen —
            // a developer applying the Identity migration by hand, setting a name, and only then
            // applying this one; and a profile write whose outbox row parked as Failed while the
            // consumer was down.
            //
            // SET LOCAL app.is_system MUST stay in the same migrationBuilder.Sql call as the
            // UPDATE: SET LOCAL is scoped to the surrounding transaction, and EF runs the
            // migration inside one. It is needed on both sides — identity.users' policy carries
            // an `OR app.is_system` arm so the SELECT sees every tenant's rows, and
            // budgeting.user_lookup's separate user_lookup_system_access policy admits the
            // cross-tenant write. Without it the UPDATE silently matches nothing and the
            // migration "succeeds" having done nothing.
            //
            // IS DISTINCT FROM rather than <> so a row whose name is being set from NULL is
            // matched; re-running touches no rows.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';
                UPDATE budgeting.user_lookup l
                SET full_name = u.full_name, updated_at_utc = now()
                FROM identity.users u
                WHERE u.id = l.user_id AND u.full_name IS DISTINCT FROM l.full_name;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the column discards the backfilled values with it, which is correct: the
            // data is new, and Identity remains the source of truth either way. As with
            // BackfillBudgetingUserLookup, the backfill half is not separately reversible — it
            // cannot know which rows it set versus which the event handler set afterwards.
            migrationBuilder.DropColumn(
                name: "full_name",
                schema: "budgeting",
                table: "user_lookup");
        }
    }
}
