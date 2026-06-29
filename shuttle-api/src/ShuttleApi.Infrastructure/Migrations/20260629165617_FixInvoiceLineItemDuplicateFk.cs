using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixInvoiceLineItemDuplicateFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoice_line_items_invoices_InvoiceId1",
                table: "invoice_line_items");

            migrationBuilder.DropIndex(
                name: "IX_invoice_line_items_InvoiceId1",
                table: "invoice_line_items");

            migrationBuilder.DropColumn(
                name: "InvoiceId1",
                table: "invoice_line_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId1",
                table: "invoice_line_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_line_items_InvoiceId1",
                table: "invoice_line_items",
                column: "InvoiceId1");

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_line_items_invoices_InvoiceId1",
                table: "invoice_line_items",
                column: "InvoiceId1",
                principalTable: "invoices",
                principalColumn: "Id");
        }
    }
}
