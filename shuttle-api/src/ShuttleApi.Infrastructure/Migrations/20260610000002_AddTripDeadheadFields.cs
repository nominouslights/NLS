using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShuttleApi.Infrastructure.Persistence;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260610000002_AddTripDeadheadFields")]
    public partial class AddTripDeadheadFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeadhead",
                table: "trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeadheadBillable",
                table: "trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeadhead",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "IsDeadheadBillable",
                table: "trips");
        }
    }
}
