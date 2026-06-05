using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShuttleApi.Infrastructure.Persistence;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260604000001_AddIsAddedAfterDepartureToPassengers")]
    public partial class AddIsAddedAfterDepartureToPassengers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAddedAfterDeparture",
                table: "trip_passengers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAddedAfterDeparture",
                table: "trip_passengers");
        }
    }
}
