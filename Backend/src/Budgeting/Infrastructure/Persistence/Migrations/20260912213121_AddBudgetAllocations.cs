using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "budget_allocations",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    justification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_allocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_budget_allocations",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_cad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    justification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_budget_allocations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budget_allocations_tenant_id_budget_code_id",
                schema: "budgeting",
                table: "budget_allocations",
                columns: new[] { "tenant_id", "budget_code_id" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_allocations_tenant_id_code",
                schema: "budgeting",
                table: "budget_allocations",
                columns: new[] { "tenant_id", "code" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "budget_allocations",
                columns: new[] { "tenant_id", "period_id", "budget_code_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_budget_allocations_tenant_id_period_id_budget_code_id",
                schema: "budgeting",
                table: "rm_budget_allocations",
                columns: new[] { "tenant_id", "period_id", "budget_code_id" },
                unique: true);

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // EF never generates RLS. Both new tables are tenant-scoped, so both get a policy —
            // rm_budget_allocations carries its own tenant_id and its own policy rather than
            // inheriting isolation from the write table it projects. Protection-by-association is
            // exactly what the convention forbids.
            //
            // Shapes match the AddBudgetCodes migration's, one per table kind:
            //
            //   budget_allocations (write side)    tenant policy + an app.is_system SELECT bypass,
            //                                      because the tenant-less projection worker reads
            //                                      write-side rows to build the read models.
            //   rm_budget_allocations (read side)  one combined policy — tenant OR app.is_system —
            //                                      so the worker and the rebuilder can write across
            //                                      tenants.
            //
            // FORCE is not optional: migrations run as the app role, which owns these tables, and
            // an unforced policy does not bind the owner. Neither is the NULLIF: current_setting
            // returns '' rather than NULL on a pooled connection Npgsql has reset, and casting ''
            // to uuid fails with 22P02 — which breaks every tenant-less session, not just that one.
            migrationBuilder.Sql(
                """
                ALTER TABLE budgeting.budget_allocations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.budget_allocations FORCE ROW LEVEL SECURITY;
                CREATE POLICY budget_allocations_tenant_isolation ON budgeting.budget_allocations
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY budget_allocations_system_read ON budgeting.budget_allocations
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.rm_budget_allocations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.rm_budget_allocations FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_budget_allocations_tenant_isolation ON budgeting.rm_budget_allocations
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_allocations",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "rm_budget_allocations",
                schema: "budgeting");
        }
    }
}
