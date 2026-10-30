using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Clients.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives each purchase order its own pricing terms: <c>round_trip_rate_cad</c> and
    /// <c>one_way_rate_cad</c> on <c>clients.purchase_orders</c> and its read-model mirror
    /// <c>clients.rm_purchase_orders</c>. Both nullable with no default and no backfill — null
    /// means "no PO term", so every existing PO keeps invoicing at the contract rate exactly as
    /// before. Purely additive: nothing is dropped, renamed, or made NOT NULL, so applying this
    /// ahead of the code is harmless. Both rates are tax-inclusive; the platform computes no tax.
    /// </summary>
    public partial class AddPurchaseOrderPricingTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "one_way_rate_cad",
                schema: "clients",
                table: "rm_purchase_orders",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "round_trip_rate_cad",
                schema: "clients",
                table: "rm_purchase_orders",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "one_way_rate_cad",
                schema: "clients",
                table: "purchase_orders",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "round_trip_rate_cad",
                schema: "clients",
                table: "purchase_orders",
                type: "numeric(12,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "one_way_rate_cad",
                schema: "clients",
                table: "rm_purchase_orders");

            migrationBuilder.DropColumn(
                name: "round_trip_rate_cad",
                schema: "clients",
                table: "rm_purchase_orders");

            migrationBuilder.DropColumn(
                name: "one_way_rate_cad",
                schema: "clients",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "round_trip_rate_cad",
                schema: "clients",
                table: "purchase_orders");
        }
    }
}
