using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleMotiveId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "motive_vehicle_id",
                schema: "fleet",
                table: "vehicles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motive_vehicle_id",
                schema: "fleet",
                table: "rm_vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_tenant_id_motive_vehicle_id",
                schema: "fleet",
                table: "vehicles",
                columns: new[] { "tenant_id", "motive_vehicle_id" },
                unique: true,
                filter: "motive_vehicle_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vehicles_tenant_id_motive_vehicle_id",
                schema: "fleet",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "motive_vehicle_id",
                schema: "fleet",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "motive_vehicle_id",
                schema: "fleet",
                table: "rm_vehicles");
        }
    }
}
