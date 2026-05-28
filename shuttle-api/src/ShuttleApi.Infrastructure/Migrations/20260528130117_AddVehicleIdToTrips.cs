using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleIdToTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "trips",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "trips");
        }
    }
}
