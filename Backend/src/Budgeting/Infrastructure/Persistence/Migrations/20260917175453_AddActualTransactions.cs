using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActualTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "actual_transactions",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    external_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actual_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_actual_transactions",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    external_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_actual_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_actual_transactions_tenant_id_budget_code_id",
                schema: "budgeting",
                table: "actual_transactions",
                columns: new[] { "tenant_id", "budget_code_id" });

            migrationBuilder.CreateIndex(
                name: "IX_actual_transactions_tenant_id_occurred_on",
                schema: "budgeting",
                table: "actual_transactions",
                columns: new[] { "tenant_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "IX_actual_transactions_tenant_id_source_external_reference",
                schema: "budgeting",
                table: "actual_transactions",
                columns: new[] { "tenant_id", "source", "external_reference" },
                unique: true,
                filter: "external_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rm_actual_transactions_tenant_id_budget_code_id",
                schema: "budgeting",
                table: "rm_actual_transactions",
                columns: new[] { "tenant_id", "budget_code_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_actual_transactions_tenant_id_occurred_on",
                schema: "budgeting",
                table: "rm_actual_transactions",
                columns: new[] { "tenant_id", "occurred_on" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // EF never generates RLS. Both new tables are tenant-scoped, so both get a policy —
            // rm_actual_transactions carries its own tenant_id and its own policy rather than
            // inheriting isolation from the write table it projects. Protection-by-association is
            // exactly what the convention forbids.
            //
            // Shapes match the AddBudgetAllocations migration's, one per table kind:
            //
            //   actual_transactions (write side)    tenant policy + an app.is_system SELECT bypass,
            //                                       because the tenant-less projection worker reads
            //                                       write-side rows to build the read models.
            //   rm_actual_transactions (read side)  one combined policy — tenant OR app.is_system —
            //                                       so the worker and the rebuilder can write across
            //                                       tenants.
            //
            // FORCE is not optional: migrations run as the app role, which owns these tables, and
            // an unforced policy does not bind the owner. Neither is the NULLIF: current_setting
            // returns '' rather than NULL on a pooled connection Npgsql has reset, and casting ''
            // to uuid fails with 22P02 — which breaks every tenant-less session, not just that one.
            //
            // Plain tables, as everywhere else in this schema: no view over the data and no second
            // database role. The policy is the boundary, and it is one thing to read.
            migrationBuilder.Sql(
                """
                ALTER TABLE budgeting.actual_transactions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.actual_transactions FORCE ROW LEVEL SECURITY;
                CREATE POLICY actual_transactions_tenant_isolation ON budgeting.actual_transactions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY actual_transactions_system_read ON budgeting.actual_transactions
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.rm_actual_transactions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.rm_actual_transactions FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_actual_transactions_tenant_isolation ON budgeting.rm_actual_transactions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actual_transactions",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "rm_actual_transactions",
                schema: "budgeting");
        }
    }
}
