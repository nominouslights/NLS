using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Budget items: a period may now hold many items per budget code (a code's budget is the sum
    /// of its items), each with a title, an optional quantity × unit cost build-up, a unit,
    /// classification (spend type, recurrence), vendor, tags, priority, assumptions and the
    /// consequence if unfunded. Additive only — no column is dropped or renamed:
    /// <list type="bullet">
    /// <item>the unique (tenant_id, period_id, budget_code_id) index on both tables becomes a
    /// non-unique index of the same name and columns;</item>
    /// <item>eleven columns are added to both tables. The three enum columns and <c>tags</c> carry
    /// DB defaults that were added to this file by hand (<c>'Operating'</c>, <c>'OneTime'</c>,
    /// <c>'ShouldHave'</c>, <c>'{}'</c>) so existing rows backfill to valid values. They are
    /// deliberately NOT in the EF model — see <c>BudgetAllocationConfiguration</c> for why a model
    /// default on Priority would corrupt MustHave inserts;</item>
    /// <item>existing rows get <c>title = code</c>.</item>
    /// </list>
    /// RLS policies are unchanged: same tables, same tenant_id column.
    /// </summary>
    public partial class AddBudgetItemDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rm_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropIndex(
                name: "IX_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.AddColumn<string>(
                name: "assumptions",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "consequence_if_unfunded",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "ShouldHave");

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recurrence",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "OneTime");

            migrationBuilder.AddColumn<string>(
                name: "spend_type",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Operating");

            migrationBuilder.AddColumn<List<string>>(
                name: "tags",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "unit",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_cost_cad",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vendor",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assumptions",
                schema: "budgeting",
                table: "budget_allocations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "consequence_if_unfunded",
                schema: "budgeting",
                table: "budget_allocations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority",
                schema: "budgeting",
                table: "budget_allocations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "ShouldHave");

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                schema: "budgeting",
                table: "budget_allocations",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recurrence",
                schema: "budgeting",
                table: "budget_allocations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "OneTime");

            migrationBuilder.AddColumn<string>(
                name: "spend_type",
                schema: "budgeting",
                table: "budget_allocations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Operating");

            migrationBuilder.AddColumn<List<string>>(
                name: "tags",
                schema: "budgeting",
                table: "budget_allocations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "budgeting",
                table: "budget_allocations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "unit",
                schema: "budgeting",
                table: "budget_allocations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_cost_cad",
                schema: "budgeting",
                table: "budget_allocations",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vendor",
                schema: "budgeting",
                table: "budget_allocations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            // Existing lines predate titles: give each its code string so it reads sensibly until
            // a planner writes a real one. Both tables, so the read side agrees without waiting
            // for a projection rebuild. Only rows still on the '' column default are touched.
            migrationBuilder.Sql(
                "UPDATE budgeting.budget_allocations SET title = code WHERE title = '';");
            migrationBuilder.Sql(
                "UPDATE budgeting.rm_budget_allocations SET title = code WHERE title = '';");

            migrationBuilder.CreateIndex(
                name: "IX_rm_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "rm_budget_allocations",
                columns: new[] { "tenant_id", "period_id", "budget_code_id" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "budget_allocations",
                columns: new[] { "tenant_id", "period_id", "budget_code_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rm_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropIndex(
                name: "IX_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "assumptions",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "consequence_if_unfunded",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "priority",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "quantity",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "recurrence",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "spend_type",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "tags",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "unit",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "unit_cost_cad",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "vendor",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "assumptions",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "consequence_if_unfunded",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "priority",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "quantity",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "recurrence",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "spend_type",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "tags",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "unit",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "unit_cost_cad",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.DropColumn(
                name: "vendor",
                schema: "budgeting",
                table: "budget_allocations");

            migrationBuilder.CreateIndex(
                name: "IX_rm_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "rm_budget_allocations",
                columns: new[] { "tenant_id", "period_id", "budget_code_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "budget_allocations",
                columns: new[] { "tenant_id", "period_id", "budget_code_id" },
                unique: true);
        }
    }
}
