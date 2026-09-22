using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Billing.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds <c>billing.purchase_order_snapshots</c> — Billing's replica of a client purchase
    /// order, carrying that PO's own negotiated round-trip and one-way rates so a draft invoice
    /// can price from the PO with the contract rate only as fallback. Maintained by the
    /// <c>clients.purchase-order-changed</c> / <c>clients.purchase-order-deleted</c> consumers;
    /// Billing never queries the Clients module.
    /// <para>
    /// Additive: a new table only. The table is tenant-scoped, so RLS goes on in this same
    /// migration — <c>ENABLE</c> + <c>FORCE ROW LEVEL SECURITY</c> plus the two canonical
    /// policies, on a plain table, exactly as <c>InitialClientsSchema</c> does it. <c>FORCE</c>
    /// is required because migrations run as the table owner, and the
    /// <c>NULLIF(current_setting(…, true), '')::uuid</c> form is what survives a pooled
    /// connection's <c>DISCARD ALL</c> resetting the GUC to an empty string.
    /// </para>
    /// </summary>
    public partial class AddPurchaseOrderSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_order_snapshots",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    amount_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    round_trip_rate_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    one_way_rate_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_snapshots_tenant_id_client_id_po_number",
                schema: "billing",
                table: "purchase_order_snapshots",
                columns: new[] { "tenant_id", "client_id", "po_number" });

            // Tenant isolation, database half. Never a view, never a second role.
            migrationBuilder.Sql("""
                ALTER TABLE billing.purchase_order_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.purchase_order_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY purchase_order_snapshots_tenant_isolation ON billing.purchase_order_snapshots
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY purchase_order_snapshots_system_read ON billing.purchase_order_snapshots
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Policies first — dropping the table would take them with it, but being explicit
            // keeps Down reversible if the table drop is ever split out.
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS purchase_order_snapshots_system_read ON billing.purchase_order_snapshots;
                DROP POLICY IF EXISTS purchase_order_snapshots_tenant_isolation ON billing.purchase_order_snapshots;
                """);

            migrationBuilder.DropTable(
                name: "purchase_order_snapshots",
                schema: "billing");
        }
    }
}
