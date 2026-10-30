using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCostCentres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cost_centres",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_centres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_cost_centres",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_cost_centres", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budget_codes_tenant_id_cost_centre",
                schema: "budgeting",
                table: "budget_codes",
                columns: new[] { "tenant_id", "cost_centre" });

            migrationBuilder.CreateIndex(
                name: "IX_cost_centres_tenant_id_code",
                schema: "budgeting",
                table: "cost_centres",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cost_centres_tenant_id_parent_id",
                schema: "budgeting",
                table: "cost_centres",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_cost_centres_tenant_id_code",
                schema: "budgeting",
                table: "rm_cost_centres",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // EF never generates RLS. Both new tables are tenant-scoped, so both get a policy —
            // rm_cost_centres carries its own tenant_id and its own policy rather than inheriting
            // isolation from the write table it projects. Shapes copied from AddBudgetAllocations,
            // one per table kind:
            //
            //   cost_centres (write side)    tenant policy + an app.is_system SELECT bypass,
            //                                because the tenant-less projection worker reads
            //                                write-side rows to build the read model.
            //   rm_cost_centres (read side)  one combined policy — tenant OR app.is_system — so
            //                                the worker and the rebuilder can write across tenants.
            //
            // FORCE is not optional: migrations run as the app role, which owns these tables, and
            // an unforced policy does not bind the owner. Neither is the NULLIF: current_setting
            // returns '' rather than NULL on a pooled connection Npgsql has reset, and casting ''
            // to uuid fails with 22P02.
            migrationBuilder.Sql(
                """
                ALTER TABLE budgeting.cost_centres ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.cost_centres FORCE ROW LEVEL SECURITY;
                CREATE POLICY cost_centres_tenant_isolation ON budgeting.cost_centres
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY cost_centres_system_read ON budgeting.cost_centres
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.rm_cost_centres ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.rm_cost_centres FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_cost_centres_tenant_isolation ON budgeting.rm_cost_centres
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);

            // ---- Hand-appended: backfill the register from the cost centres already in use ----
            // Budget codes now validate their cost centre against this register, so every value
            // already on a code must have an entry, or the next edit of that code would fail
            // CostCentreNotFound. One entry per (tenant, distinct cost_centre), across every
            // period: Code = Name = the stored string (verbatim — it is already trimmed by
            // BudgetCode.Apply, and the register matches it ordinally), active, no owner, no
            // parent, no actor (created_by NULL: the system did this, not a person), version 1.
            //
            // Raw SQL bypasses the audit pipeline, so no event_journal row is written and the
            // projection worker will never see these aggregates — hence rm_cost_centres is
            // written directly too (the BackfillBudgetingUserLookup / ClearCostCentreOnRevenue
            // precedent). An entry's journal history starts at its first edit through the API.
            //
            // RLS is the whole difficulty, and getting it wrong is silent: a migration runs with no
            // app.tenant_id, so a tenant policy matches nothing and the INSERT "succeeds" having
            // done nothing. Everything is ONE Sql batch because SET LOCAL is scoped to the
            // migration's transaction:
            //
            //   budget_codes (read)      budget_codes_system_read admits the SELECT under
            //                            app.is_system.
            //   cost_centres (write)     its only system policy is FOR SELECT, so FORCE is dropped
            //                            for the INSERT (the app role owns the table; an unforced
            //                            policy does not bind the owner) and restored in the same
            //                            transaction — ALTER TABLE is transactional, so a failure
            //                            rolls everything back and the table is never left
            //                            unforced. No policy is created, altered or dropped here.
            //   rm_cost_centres (write)  its policy carries an `OR app.is_system` arm for every
            //                            command, so app.is_system admits the INSERT.
            //
            // Idempotent: ON CONFLICT DO NOTHING on (tenant_id, code) and on the read row's id.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';

                ALTER TABLE budgeting.cost_centres NO FORCE ROW LEVEL SECURITY;
                INSERT INTO budgeting.cost_centres
                    (id, tenant_id, code, name, description, owner_user_id, parent_id, is_active,
                     created_by, modified_by, created_at_utc, updated_at_utc, version)
                SELECT gen_random_uuid(), used.tenant_id, used.cost_centre, used.cost_centre,
                       NULL, NULL, NULL, true, NULL, NULL, now(), now(), 1
                FROM (SELECT DISTINCT tenant_id, cost_centre
                      FROM budgeting.budget_codes
                      WHERE cost_centre IS NOT NULL AND btrim(cost_centre) <> '') AS used
                ON CONFLICT (tenant_id, code) DO NOTHING;
                ALTER TABLE budgeting.cost_centres FORCE ROW LEVEL SECURITY;

                INSERT INTO budgeting.rm_cost_centres
                    (id, tenant_id, code, name, description, owner_user_id, parent_id, is_active,
                     created_by, modified_by, created_at_utc, updated_at_utc, version)
                SELECT id, tenant_id, code, name, description, owner_user_id, parent_id, is_active,
                       created_by, modified_by, created_at_utc, updated_at_utc, version
                FROM budgeting.cost_centres
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cost_centres",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "rm_cost_centres",
                schema: "budgeting");

            migrationBuilder.DropIndex(
                name: "IX_budget_codes_tenant_id_cost_centre",
                schema: "budgeting",
                table: "budget_codes");
        }
    }
}
