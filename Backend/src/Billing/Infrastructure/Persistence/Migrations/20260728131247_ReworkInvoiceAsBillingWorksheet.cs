using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReworkInvoiceAsBillingWorksheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Collapse the lifecycle: Sent/Paid both become EnteredInQbo before the
            // receivables columns are dropped. Columns only — the RLS policy blocks are untouched.
            migrationBuilder.Sql(
                "UPDATE billing.invoices SET status = 'EnteredInQbo' WHERE status IN ('Sent', 'Paid');");
            migrationBuilder.Sql(
                "UPDATE billing.rm_invoices SET status = 'EnteredInQbo' WHERE status IN ('Sent', 'Paid');");

            migrationBuilder.DropColumn(
                name: "paid_at_utc",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "qbo_sync_status",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "sent_at_utc",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "paid_at_utc",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "qbo_sync_status",
                schema: "billing",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "sent_at_utc",
                schema: "billing",
                table: "invoices");

            migrationBuilder.AddColumn<DateOnly>(
                name: "qbo_entered_date",
                schema: "billing",
                table: "rm_invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "qbo_entered_date",
                schema: "billing",
                table: "invoices",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "qbo_entered_date",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "qbo_entered_date",
                schema: "billing",
                table: "invoices");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at_utc",
                schema: "billing",
                table: "rm_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qbo_sync_status",
                schema: "billing",
                table: "rm_invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sent_at_utc",
                schema: "billing",
                table: "rm_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at_utc",
                schema: "billing",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qbo_sync_status",
                schema: "billing",
                table: "invoices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sent_at_utc",
                schema: "billing",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            // Best-effort reverse of the lifecycle collapse (Paid is unrecoverable).
            migrationBuilder.Sql(
                "UPDATE billing.invoices SET status = 'Sent' WHERE status = 'EnteredInQbo';");
            migrationBuilder.Sql(
                "UPDATE billing.rm_invoices SET status = 'Sent' WHERE status = 'EnteredInQbo';");
        }
    }
}
