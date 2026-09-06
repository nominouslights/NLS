using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDayLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "minimum_guaranteed",
                schema: "booking",
                table: "booking_days",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reverted_at_utc",
                schema: "booking",
                table: "booking_days",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "booking",
                table: "booking_days",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Unconfirmed");

            migrationBuilder.AddColumn<string>(
                name: "trip_number",
                schema: "booking",
                table: "booking_days",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "minimum_guaranteed",
                schema: "booking",
                table: "booking_days");

            migrationBuilder.DropColumn(
                name: "reverted_at_utc",
                schema: "booking",
                table: "booking_days");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "booking",
                table: "booking_days");

            migrationBuilder.DropColumn(
                name: "trip_number",
                schema: "booking",
                table: "booking_days");
        }
    }
}
