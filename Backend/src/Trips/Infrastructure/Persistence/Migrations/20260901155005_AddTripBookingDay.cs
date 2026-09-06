using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripBookingDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "booking_day_id",
                schema: "trips",
                table: "trips",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_trips_tenant_id_booking_day_id",
                schema: "trips",
                table: "trips",
                columns: new[] { "tenant_id", "booking_day_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trips_tenant_id_booking_day_id",
                schema: "trips",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "booking_day_id",
                schema: "trips",
                table: "trips");
        }
    }
}
