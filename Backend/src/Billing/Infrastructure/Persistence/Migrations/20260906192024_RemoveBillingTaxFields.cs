using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBillingTaxFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gst_applicable",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "gst_cad",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "gst_rate",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "subtotal_cad",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "gst_applicable",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "gst_rate",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "gst_applicable",
                schema: "billing",
                table: "contract_snapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "gst_applicable",
                schema: "billing",
                table: "rm_invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "gst_cad",
                schema: "billing",
                table: "rm_invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "gst_rate",
                schema: "billing",
                table: "rm_invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "subtotal_cad",
                schema: "billing",
                table: "rm_invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "gst_applicable",
                schema: "billing",
                table: "invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "gst_rate",
                schema: "billing",
                table: "invoices",
                type: "numeric(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "gst_applicable",
                schema: "billing",
                table: "contract_snapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
