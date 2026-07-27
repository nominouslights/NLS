using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripHasPostTripInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_post_trip_inspection",
                schema: "trips",
                table: "trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_post_trip_inspection",
                schema: "trips",
                table: "rm_trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_post_trip_inspection",
                schema: "trips",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "has_post_trip_inspection",
                schema: "trips",
                table: "rm_trips");
        }
    }
}
