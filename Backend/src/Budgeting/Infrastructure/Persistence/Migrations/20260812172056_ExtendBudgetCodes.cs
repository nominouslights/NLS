using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// US-6.1.1 — widens BudgetCode from the first slice's seven fields to the story's full set:
    /// classification (service line, cost centre, parent), accounting (GL account, tax treatment)
    /// and governance (budget owner, review frequency, created/modified by).
    ///
    /// Every change is applied to BOTH budget_codes and rm_budget_codes. The read model mirrors
    /// the write side column for column, and a projection that writes a column the read table
    /// lacks fails at runtime, not at build.
    ///
    /// TWO HAND-EDITS were made to the scaffolded output; do not regenerate this file over them:
    ///
    ///   1. EF emitted DropColumn("justification") + AddColumn("description"), which destroys the
    ///      content. Replaced with RenameColumn + AlterColumn so the text survives. (The tables
    ///      happened to be empty when this was written — verified, zero rows in both — but a
    ///      migration that only works on an empty table is a trap for the next environment.)
    ///   2. EF defaulted the new NOT NULL review_frequency to "", which is not a member of
    ///      BudgetReviewFrequency. Changed to "Quarterly", the documented default, and the
    ///      residual DEFAULT is dropped at the end (see below).
    /// </summary>
    public partial class ExtendBudgetCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- justification → description, and it becomes optional. -------------------------
            // Renamed rather than dropped-and-re-added: same content, same width, and the text a
            // planner wrote is worth keeping. The field changes meaning slightly and deliberately
            // — architecture §5.3 says a code is re-justified *each period*, so the recurring
            // justification belongs on the allocation (Stage 6.2). What stays on the code is the
            // standing "what this covers" note, which is why it is no longer required.
            migrationBuilder.RenameColumn(
                name: "justification",
                schema: "budgeting",
                table: "budget_codes",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "justification",
                schema: "budgeting",
                table: "rm_budget_codes",
                newName: "description");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "budgeting",
                table: "budget_codes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "budgeting",
                table: "rm_budget_codes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            // --- stream and owner are replaced, not migrated. ---------------------------------
            // stream was required free text from architecture §5.3's parent-stream list
            // ("Crew Coordination Services", "Alamos Contract", "NIHB", "Fleet Maintenance"). It
            // is superseded by service_line, a closed enum whose six revenue members are the same
            // strings Trips and Billing already emit — the point being that revenue-mix reporting
            // joins on an existing value instead of a translation table. The old values are NOT
            // translated: they do not map cleanly ("Crew Coordination Services" is arguably
            // ContractCrew and arguably Administrative), and a guessed service line silently
            // misattributes the revenue mix the enum exists to compute. A NULL renders as
            // "Unassigned" and a human fixes it in seconds.
            //
            // owner was a free-text name. Translation is not merely unwise but impossible:
            // identity.users has no name field — email is the only human-readable identifier an
            // account has — so "R. Kelsey" cannot resolve to a user id by any rule. The
            // replacement, budget_owner_user_id, is validated against budgeting.user_lookup.
            //
            // Verified before writing: budget_codes and rm_budget_codes both held zero rows, so
            // nothing was actually discarded here.
            migrationBuilder.DropColumn(name: "stream", schema: "budgeting", table: "budget_codes");
            migrationBuilder.DropColumn(name: "owner", schema: "budgeting", table: "budget_codes");
            migrationBuilder.DropColumn(name: "stream", schema: "budgeting", table: "rm_budget_codes");
            migrationBuilder.DropColumn(name: "owner", schema: "budgeting", table: "rm_budget_codes");

            // --- Classification, accounting and governance. -----------------------------------
            foreach (var table in new[] { "budget_codes", "rm_budget_codes" })
            {
                // 32 deliberately matches trips.service_type / clients.service_type: these hold
                // the same strings and are meant to join, so neither side may truncate first.
                migrationBuilder.AddColumn<string>(
                    name: "service_line", schema: "budgeting", table: table,
                    type: "character varying(32)", maxLength: 32, nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "cost_centre", schema: "budgeting", table: table,
                    type: "character varying(32)", maxLength: 32, nullable: true);

                migrationBuilder.AddColumn<Guid>(
                    name: "parent_code_id", schema: "budgeting", table: table,
                    type: "uuid", nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "gl_account_code", schema: "budgeting", table: table,
                    type: "character varying(32)", maxLength: 32, nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "tax_treatment", schema: "budgeting", table: table,
                    type: "character varying(24)", maxLength: 24, nullable: true);

                migrationBuilder.AddColumn<Guid>(
                    name: "budget_owner_user_id", schema: "budgeting", table: table,
                    type: "uuid", nullable: true);

                // The only new NOT NULL column. The default backfills existing rows and is
                // dropped immediately afterwards — see the SQL block below.
                migrationBuilder.AddColumn<string>(
                    name: "review_frequency", schema: "budgeting", table: table,
                    type: "character varying(16)", maxLength: 16, nullable: false,
                    defaultValue: "Quarterly");

                migrationBuilder.AddColumn<Guid>(
                    name: "created_by", schema: "budgeting", table: table,
                    type: "uuid", nullable: true);

                migrationBuilder.AddColumn<Guid>(
                    name: "modified_by", schema: "budgeting", table: table,
                    type: "uuid", nullable: true);
            }

            // Write side only: HasChildrenAsync runs in the update and delete handlers, both of
            // which work against the aggregate.
            migrationBuilder.CreateIndex(
                name: "IX_budget_codes_tenant_id_parent_code_id",
                schema: "budgeting",
                table: "budget_codes",
                columns: new[] { "tenant_id", "parent_code_id" });

            // Read side only, and forward-looking: Stage 6.2's revenue-mix report groups
            // rm_budget_codes by service line. Nothing queries it today.
            migrationBuilder.CreateIndex(
                name: "IX_rm_budget_codes_tenant_id_service_line",
                schema: "budgeting",
                table: "rm_budget_codes",
                columns: new[] { "tenant_id", "service_line" });

            // ---- Hand-appended ----------------------------------------------------------------
            // AddColumn(nullable: false, defaultValue: …) leaves a DEFAULT on the column that the
            // EF model snapshot does not record. That is silent schema drift: the next scaffolded
            // migration would not know it exists, and an INSERT omitting review_frequency would
            // quietly succeed instead of failing the way the model says it should. Drop it now
            // that the (zero) existing rows are backfilled.
            //
            // No RLS block here, and that is not an oversight: budget_codes and rm_budget_codes
            // already carry their policies from AddBudgetCodes, and adding or dropping columns
            // does not disturb them. Only a NEW table needs the ENABLE/FORCE/policy treatment —
            // which is why AddBudgetingUserLookup has one and this does not.
            migrationBuilder.Sql(
                """
                ALTER TABLE budgeting.budget_codes    ALTER COLUMN review_frequency DROP DEFAULT;
                ALTER TABLE budgeting.rm_budget_codes ALTER COLUMN review_frequency DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_budget_codes_tenant_id_parent_code_id",
                schema: "budgeting",
                table: "budget_codes");

            migrationBuilder.DropIndex(
                name: "IX_rm_budget_codes_tenant_id_service_line",
                schema: "budgeting",
                table: "rm_budget_codes");

            foreach (var table in new[] { "budget_codes", "rm_budget_codes" })
            {
                migrationBuilder.DropColumn(name: "service_line", schema: "budgeting", table: table);
                migrationBuilder.DropColumn(name: "cost_centre", schema: "budgeting", table: table);
                migrationBuilder.DropColumn(name: "parent_code_id", schema: "budgeting", table: table);
                migrationBuilder.DropColumn(name: "gl_account_code", schema: "budgeting", table: table);
                migrationBuilder.DropColumn(name: "tax_treatment", schema: "budgeting", table: table);
                migrationBuilder.DropColumn(name: "budget_owner_user_id", schema: "budgeting", table: table);
                migrationBuilder.DropColumn(name: "review_frequency", schema: "budgeting", table: table);
                migrationBuilder.DropColumn(name: "created_by", schema: "budgeting", table: table);
                migrationBuilder.DropColumn(name: "modified_by", schema: "budgeting", table: table);

                // stream and owner come back empty. Their values were not translated forward and
                // cannot be recovered — reverting restores the shape, not the data.
                migrationBuilder.AddColumn<string>(
                    name: "stream", schema: "budgeting", table: table,
                    type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "");

                migrationBuilder.AddColumn<string>(
                    name: "owner", schema: "budgeting", table: table,
                    type: "character varying(80)", maxLength: 80, nullable: true);
            }

            // A description that is null cannot become a NOT NULL justification, so fill the gap
            // before tightening the column.
            migrationBuilder.Sql(
                """
                UPDATE budgeting.budget_codes    SET description = '' WHERE description IS NULL;
                UPDATE budgeting.rm_budget_codes SET description = '' WHERE description IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "budgeting",
                table: "budget_codes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "budgeting",
                table: "rm_budget_codes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "description",
                schema: "budgeting",
                table: "budget_codes",
                newName: "justification");

            migrationBuilder.RenameColumn(
                name: "description",
                schema: "budgeting",
                table: "rm_budget_codes",
                newName: "justification");
        }
    }
}
