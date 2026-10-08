using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rm_vendors",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    gst_registration_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    qbo_display_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_budget_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_vendors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vendors",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    gst_registration_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    qbo_display_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_budget_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendors", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rm_vendors_tenant_id_normalized_name",
                schema: "budgeting",
                table: "rm_vendors",
                columns: new[] { "tenant_id", "normalized_name" });

            migrationBuilder.CreateIndex(
                name: "IX_vendors_tenant_id_normalized_name",
                schema: "budgeting",
                table: "vendors",
                columns: new[] { "tenant_id", "normalized_name" },
                unique: true);

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // EF never generates RLS. Both new tables are tenant-scoped, so both get a policy —
            // rm_vendors carries its own tenant_id and its own policy rather than inheriting
            // isolation from the write table it projects.
            //
            // Shapes match the AddBudgetAllocations migration's, one per table kind:
            //
            //   vendors (write side)     tenant policy + an app.is_system SELECT bypass, because
            //                            the tenant-less projection worker reads write-side rows
            //                            to build the read model.
            //   rm_vendors (read side)   one combined policy — tenant OR app.is_system — so the
            //                            worker and the rebuilder can write across tenants.
            //
            // FORCE is not optional: migrations run as the app role, which owns these tables, and
            // an unforced policy does not bind the owner. Neither is the NULLIF: current_setting
            // returns '' rather than NULL on a pooled connection Npgsql has reset, and casting ''
            // to uuid fails with 22P02 — which breaks every tenant-less session, not just that one.
            migrationBuilder.Sql(
                """
                ALTER TABLE budgeting.vendors ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.vendors FORCE ROW LEVEL SECURITY;
                CREATE POLICY vendors_tenant_isolation ON budgeting.vendors
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY vendors_system_read ON budgeting.vendors
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.rm_vendors ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.rm_vendors FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_vendors_tenant_isolation ON budgeting.rm_vendors
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rm_vendors",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "vendors",
                schema: "budgeting");
        }
    }
}
