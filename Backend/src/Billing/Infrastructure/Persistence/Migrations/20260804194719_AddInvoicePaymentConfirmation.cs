using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePaymentConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "payment_confirmed_date",
                schema: "billing",
                table: "rm_invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "payment_confirmed_date",
                schema: "billing",
                table: "invoices",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_confirmed_date",
                schema: "billing",
                table: "rm_invoices");

            migrationBuilder.DropColumn(
                name: "payment_confirmed_date",
                schema: "billing",
                table: "invoices");
        }
    }
}
