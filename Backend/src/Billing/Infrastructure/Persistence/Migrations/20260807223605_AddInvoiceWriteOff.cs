using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Billing.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Invoice write-off: amount/date/reason on the write model and read model, plus a
    /// materialized <c>outstanding_cad</c> on <c>rm_invoices</c> so the receivables tiles are a
    /// sum rather than a per-row status test. On the write side the balance stays computed
    /// (<c>Invoice.OutstandingCad</c>); the read-model column is backfilled here because none of
    /// this writes event-journal rows, so the projector never re-runs for existing invoices.
    /// </summary>
    public partial class AddInvoiceWriteOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "outstanding_cad",
                schema: "billing",
                table: "rm_invoices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "written_off_amount_cad",
                schema: "billing",
                table: "rm_invoices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "written_off_date",
                schema: "billing",
                table: "rm_invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "written_off_reason",
                schema: "billing",
                table: "rm_invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "written_off_amount_cad",
                schema: "billing",
                table: "invoices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "written_off_date",
                schema: "billing",
                table: "invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "written_off_reason",
                schema: "billing",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Only an invoice sitting in QuickBooks unpaid is outstanding; everything else —
            // draft, void, paid — carries the column's zero default.
            migrationBuilder.Sql(
                """
                UPDATE billing.rm_invoices
                   SET outstanding_cad = total_cad
                 WHERE status = 'EnteredInQbo';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "outstanding_cad",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "written_off_amount_cad",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "written_off_date",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "written_off_reason",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "written_off_amount_cad",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "written_off_date",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "written_off_reason",
                schema: "billing",
                table: "invoices");
        }
    }
}
