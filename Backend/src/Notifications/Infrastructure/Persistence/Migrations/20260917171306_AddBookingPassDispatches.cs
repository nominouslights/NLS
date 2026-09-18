using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPassDispatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "booking_id",
                schema: "notifications",
                table: "rm_email_dispatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "booking_reference",
                schema: "notifications",
                table: "rm_email_dispatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "booking_id",
                schema: "notifications",
                table: "email_dispatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "booking_reference",
                schema: "notifications",
                table: "email_dispatches",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_email_dispatches_tenant_id_booking_id",
                schema: "notifications",
                table: "rm_email_dispatches",
                columns: new[] { "tenant_id", "booking_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rm_email_dispatches_tenant_id_booking_id",
                schema: "notifications",
                table: "rm_email_dispatches");

            migrationBuilder.DropColumn(
                name: "booking_id",
                schema: "notifications",
                table: "rm_email_dispatches");

            migrationBuilder.DropColumn(
                name: "booking_reference",
                schema: "notifications",
                table: "rm_email_dispatches");

            migrationBuilder.DropColumn(
                name: "booking_id",
                schema: "notifications",
                table: "email_dispatches");

            migrationBuilder.DropColumn(
                name: "booking_reference",
                schema: "notifications",
                table: "email_dispatches");
        }
    }
}
